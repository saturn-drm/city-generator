using System;
using System.Collections;
using System.Collections.Generic;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

using System.IO;
using System.Linq;
using System.Data;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using System.Runtime.InteropServices;

using Rhino.DocObjects;
using Rhino.Collections;
using GH_IO;
using GH_IO.Serialization;

/// <summary>
/// This class will be instantiated on demand by the Script component.
/// </summary>
public class Script_Instance : GH_ScriptInstance
{
#region Utility functions
  /// <summary>Print a String to the [Out] Parameter of the Script component.</summary>
  /// <param name="text">String to print.</param>
  private void Print(string text) { __out.Add(text); }
  /// <summary>Print a formatted String to the [Out] Parameter of the Script component.</summary>
  /// <param name="format">String format.</param>
  /// <param name="args">Formatting parameters.</param>
  private void Print(string format, params object[] args) { __out.Add(string.Format(format, args)); }
  /// <summary>Print useful information about an object instance to the [Out] Parameter of the Script component. </summary>
  /// <param name="obj">Object instance to parse.</param>
  private void Reflect(object obj) { __out.Add(GH_ScriptComponentUtilities.ReflectType_CS(obj)); }
  /// <summary>Print the signatures of all the overloads of a specific method to the [Out] Parameter of the Script component. </summary>
  /// <param name="obj">Object instance to parse.</param>
  private void Reflect(object obj, string method_name) { __out.Add(GH_ScriptComponentUtilities.ReflectType_CS(obj, method_name)); }
#endregion

#region Members
  /// <summary>Gets the current Rhino document.</summary>
  private RhinoDoc RhinoDocument;
  /// <summary>Gets the Grasshopper document that owns this script.</summary>
  private GH_Document GrasshopperDocument;
  /// <summary>Gets the Grasshopper script component that owns this script.</summary>
  private IGH_Component Component; 
  /// <summary>
  /// Gets the current iteration count. The first call to RunScript() is associated with Iteration==0.
  /// Any subsequent call within the same solution will increment the Iteration count.
  /// </summary>
  private int Iteration;
#endregion

  /// <summary>
  /// This procedure contains the user code. Input parameters are provided as regular arguments, 
  /// Output parameters as ref arguments. You don't have to assign output parameters, 
  /// they will have a default value.
  /// </summary>
  private void RunScript(List<Surface> Blocks, List<double> FAR, double BSR1, double BSR2, double BSR3, List<Curve> RdA, List<Curve> RdB, double TRatio, ref object TowerP, ref object TowerS, ref object Outline, ref object Buildings, ref object Yard)
  {
        // initialize
    // get the list of class Buildings, each item contains the outline of the building and the FAR
    List<Building> buildings = new List<Building>();
    for (int m = 0; m < Blocks.Count; m++) {
      double Vspan = Math.Floor(5 / FAR[m] + 1);
      int V = Convert.ToInt16(Vspan);
      double Uspan = Math.Floor(1 / FAR[m] + 1);
      int U = Convert.ToInt16(Uspan);
      Interval normDom = new Interval (0, 1);
      // Surface srf = Blocks[m].Faces[0];
      Surface srf = Blocks[m];
      srf.SetDomain(0, normDom);
      srf.SetDomain(1, normDom);
      double du = 1.0 / U;
      double dv = 1.0 / V;

      for (int i = 0; i < V; i++){
        for (int j = 0; j < U; j++){

          Mesh subMesh = new Mesh();
          subMesh.Vertices.Add(srf.PointAt(du * j, dv * i));
          subMesh.Vertices.Add(srf.PointAt(du * (j + 1), dv * i));
          subMesh.Vertices.Add(srf.PointAt(du * (j + 1), dv * (i + 1)));
          subMesh.Vertices.Add(srf.PointAt(du * j, dv * (i + 1)));
          subMesh.Faces.AddFace(0, 1, 2, 3);

          Polyline[] curves = subMesh.GetNakedEdges();
          Building bldg = new Building(curves[0], FAR[m]);
          buildings.Add(bldg);
        }
      }
    }

    List<Brep> outbuildings = new List<Brep>();
    List<Curve> outyards = new List<Curve>();
    List<Brep> outtests1 = new List<Brep>();
    List<Brep> outtests2 = new List<Brep>();
    // offset all the buildings
    foreach (Building bldg in buildings) {
      // check the FAR, 0.5-2
      if (bldg.FAR >= 0.5 && bldg.FAR < 2) {
        // offset
        bldg.buildingGenerator1(BSR1);
        outbuildings.Add(bldg.Structure);
      }
        // far, 2-5
      else if (bldg.FAR >= 2 && bldg.FAR < 5) {
        bldg.buildingGenerator2(BSR2);
        outbuildings.Add(bldg.Structure);
        outyards.Add(bldg.InnerBorder);
      }
        // far, 2-5
      else if (bldg.FAR >= 5 && bldg.FAR <= 20) {
        bldg.buildingGenerator3(BSR3);
        outbuildings.Add(bldg.Structure);
        outyards.Add(bldg.InnerBorder);
        // find pri/sec borders
        bldg.setPriSec(RdA, RdB);
        // towerbaseP
        bldg.getPriTowerBase(BSR3);
        // towerbaseS
        bldg.getSecTowerBase(BSR3);
        // generate towers
        bldg.towerGenerator(BSR3, TRatio);
        outtests1.Add(bldg.TowerPStructure);
        outtests2.Add(bldg.TowerSStructure);
      }
    }

    // outs
    List<Polyline> outs = new List<Polyline>();
    foreach (Building bldg in buildings) {
      outs.Add(bldg.Outline);
    }

    Outline = outs;
    Buildings = outbuildings;
    Yard = outyards;
    TowerP = outtests1;
    TowerS = outtests2;
  }

  // <Custom additional code> 
    public const double inter_tol = 0.001;
  public const double extent = 20;
  public class Building
  {
    // initialize
    public Polyline Outline;
    public double FAR;
    public Point3d Center;
    public Brep Structure;
    public Curve OuterBorder;
    public Curve InnerBorder;
    public List<Curve> PriLns;
    public List<Curve> SecLns;
    public Brep TowerP;
    public Brep TowerS;
    public Brep TowerPStructure;
    public Brep TowerSStructure;

    // constructor
    public Building (Polyline outline, double far)
    {
      this.Outline = outline;
      this.FAR = far;
      AreaMassProperties amp = AreaMassProperties.Compute(outline.ToNurbsCurve());
      this.Center = amp.Centroid;
      this.Structure = new Brep();
    }

    // generat far 0.5-2
    public void buildingGenerator1 (double density)
    {
      // density * offsetmax
      double off = (1 - density) * this.offsetMax();
      Curve[] offset = this.Outline.ToNurbsCurve().Offset(this.Center, Vector3d.ZAxis, off, inter_tol, CurveOffsetCornerStyle.Sharp);
      BrepFace basement = Brep.CreatePlanarBreps(offset[0], inter_tol)[0].Faces[0];
      double floor = Math.Floor(this.FAR / density);
      double len = this.Outline.Length;
      if (floor >= 1 && off < len / 20) {
        Brep ex = basement.CreateExtrusion(new Line(0, 0, 0, 0, 0, floor * 3).ToNurbsCurve(), true);
        this.Structure = ex;
      }
      else {
        Brep ex = basement.CreateExtrusion(new Line(0, 0, 0, 0, 0, 3).ToNurbsCurve(), true);
        this.Structure = ex;
      }
      this.OuterBorder = offset[0];
    }

    // generate far 2-5
    public void buildingGenerator2 (double density)
    {
      // offset 2m as setback and outerborder
      Curve[] outeroffset = this.Outline.ToNurbsCurve().Offset(this.Center, Vector3d.ZAxis, 2, inter_tol, CurveOffsetCornerStyle.Sharp);
      this.OuterBorder = outeroffset[0];
      // density * offsetmax
      double off = density * (this.offsetMax() - 2);
      // offset the outerborder
      Curve[] inneroffset = outeroffset[0].Offset(this.Center, Vector3d.ZAxis, off, inter_tol, CurveOffsetCornerStyle.Sharp);
      this.InnerBorder = inneroffset[0];
      // loft, extrude
      Brep[] loft = Brep.CreatePlanarBreps(new List<Curve>() {this.OuterBorder, this.InnerBorder}, inter_tol);
      double floor = Math.Floor(this.FAR / density);
      Brep ex = loft[0].Faces[0].CreateExtrusion(new Line(0, 0, 0, 0, 0, floor * 3).ToNurbsCurve(), true);
      this.Structure = ex;
    }

    // generate far 5-20
    public void buildingGenerator3 (double density)
    {
      // offset 2m
      Curve[] outeroffset = this.Outline.ToNurbsCurve().Offset(this.Center, Vector3d.ZAxis, 2, inter_tol, CurveOffsetCornerStyle.Sharp);
      this.OuterBorder = outeroffset[0];
      // density * offsetmax
      double off = density * (this.offsetMax() - 2);
      // offset the outerborder
      Curve[] inneroffset = outeroffset[0].Offset(this.Center, Vector3d.ZAxis, off, inter_tol, CurveOffsetCornerStyle.Sharp);
      this.InnerBorder = inneroffset[0];
      // loft, extrude
      Brep[] loft = Brep.CreatePlanarBreps(new List<Curve>() {this.OuterBorder, this.InnerBorder}, inter_tol);
      //double floor = Math.Floor(this.FAR / density);
      double floor = 5;
      Brep ex = loft[0].Faces[0].CreateExtrusion(new Line(0, 0, 0, 0, 0, floor * 3).ToNurbsCurve(), true);
      this.Structure = ex;
    }

    // find the shortest offset value
    public double offsetMax ()
    {
      Brep[] brp = Brep.CreatePlanarBreps(this.Outline.ToNurbsCurve(), inter_tol);
      List<double> dists = new List<double>();
      foreach (BrepEdge ed in brp[0].Edges) {
        Line ln = new Line(ed.PointAtStart, ed.PointAtEnd);
        dists.Add(ln.DistanceTo(this.Center, false));
      }
      return dists.Min();
    }

    // get borders closer to primary/secondary roads
    public void setPriSec (List<Curve> RdA, List<Curve> RdB)
    {
      //Block bl = new Block(this.Outline);
      Brep[] brp = Brep.CreatePlanarBreps(this.Outline.ToNurbsCurve(), inter_tol);
      List<Curve> primary = new List<Curve>();
      List<Curve> secondary = new List<Curve>();
      foreach (BrepEdge ed in brp[0].Edges) {
        Point3d mid = (ed.PointAtStart + ed.PointAtEnd) / 2;
        // RdA
        foreach (Curve rd in RdA) {
          Line rdln = new Line(rd.PointAtStart, rd.PointAtEnd);
          double dist = rdln.DistanceTo(mid, true);
          if (dist < 30) {
            Curve cToAdd = new Line(ed.PointAtStart, ed.PointAtEnd).ToNurbsCurve();
            primary.Add(cToAdd);
            break;
          }
          else {
            // if not break, then calc RdB
            foreach (Curve rd2 in RdB) {
              Line rdln2 = new Line(rd2.PointAtStart, rd2.PointAtEnd);
              double dist2 = rdln2.DistanceTo(mid, true);
              if (dist2 < 15) {
                Curve cToAdd = new Line(ed.PointAtStart, ed.PointAtEnd).ToNurbsCurve();
                secondary.Add(cToAdd);
                break;
              }
            }
          }
        }
      }
      this.PriLns = primary;
      this.SecLns = secondary;
    }

    // cut the area with pri/sec lines
    public void getPriTowerBase (double density)
    {
      // brp of self
      if (this.PriLns.Count == 0) { return; }
      Brep[] brp = Brep.CreatePlanarBreps(this.OuterBorder, inter_tol);
      // ensure no duplicated curves here !important
      List<Curve> joins = new List<Curve>();
      List<Point3d> start = new List<Point3d>();
      List<Point3d> end = new List<Point3d>();
      foreach (Curve cv in this.PriLns) {
        if (start.Contains(cv.PointAtStart)) { continue; }
        if (end.Contains(cv.PointAtEnd)) { continue; }
        start.Add(cv.PointAtStart);
        end.Add(cv.PointAtEnd);
        joins.Add(cv);
      }
      Curve[] c = Curve.JoinCurves(joins, inter_tol);
      double off = density * (this.offsetMax() - 2);
      Curve[] offset = c[0].Offset(this.Center, Vector3d.ZAxis, off, inter_tol, CurveOffsetCornerStyle.Sharp);
      if (!offset[0].IsValid) { return; }
      Curve extended = offset[0].Extend(CurveEnd.Both, extent, CurveExtensionStyle.Line);
      Brep[] cutted = brp[0].Split(new List<Curve> {extended}, inter_tol);
      if (cutted.Length == 0) { return; }
      cutted = cutted.Where(brptmp => brptmp != null).ToArray();
      List<double> areas = new List<double>();
      List<Brep> parts = new List<Brep>();
      foreach (Brep cut in cutted) {
        AreaMassProperties amp = AreaMassProperties.Compute(cut);
        areas.Add(amp.Area);
        parts.Add(cut);
      }
      this.TowerP = parts[areas.IndexOf(areas.Min())];
    }

    // cut the area with pri/sec lines
    public void getSecTowerBase (double density)
    {
      // brp of self
      if (this.SecLns.Count == 0) { return; }
      Brep[] brp = Brep.CreatePlanarBreps(this.OuterBorder, inter_tol);
      // ensure no duplicated curves here !important
      List<Curve> joins = new List<Curve>();
      List<Point3d> start = new List<Point3d>();
      List<Point3d> end = new List<Point3d>();
      foreach (Curve cv in this.SecLns) {
        if (start.Contains(cv.PointAtStart)) { continue; }
        if (end.Contains(cv.PointAtEnd)) { continue; }
        start.Add(cv.PointAtStart);
        end.Add(cv.PointAtEnd);
        joins.Add(cv);
      }
      Curve[] c = Curve.JoinCurves(joins, inter_tol);
      double off = density * (this.offsetMax() - 2);
      Curve[] offset = c[0].Offset(this.Center, Vector3d.ZAxis, off, inter_tol, CurveOffsetCornerStyle.Sharp);
      if (!offset[0].IsValid) { return; }
      Curve extended = offset[0].Extend(CurveEnd.Both, extent, CurveExtensionStyle.Line);
      Brep[] cutted = brp[0].Split(new List<Curve> {extended}, inter_tol);
      cutted = cutted.Where(brptmp => brptmp != null).ToArray();
      if (cutted.Length == 0) { return; }
      List<double> areas = new List<double>();
      List<Brep> parts = new List<Brep>();
      foreach (Brep cut in cutted) {
        AreaMassProperties amp = AreaMassProperties.Compute(cut);
        areas.Add(amp.Area);
        parts.Add(cut);
      }
      this.TowerS = parts[areas.IndexOf(areas.Min())];
    }

    // extrude and move the towers to the top
    public void towerGenerator (double density, double ratio)
    {
      // tower height calculation
      Brep basement = Brep.CreatePlanarBreps(new List<Curve> {this.OuterBorder, this.InnerBorder}, inter_tol)[0];
      double baseArea = basement.GetArea();
      Brep block = Brep.CreatePlanarBreps(new List<Curve> {this.Outline.ToNurbsCurve()}, inter_tol)[0];
      double blockArea = block.GetArea();
      double baseBSR = baseArea / blockArea;
      double floor = 5;
      double baseFAR = baseBSR * floor;
      double tArea = Math.Abs((this.FAR - baseFAR) * blockArea);
      // need remap the area and height
      if (tArea <= 0) { return; }
      var moveZ = Rhino.Geometry.Transform.Translation(new Vector3d(0, 0, floor * 3));
      if (this.TowerP != null) {
        // extrude and move - primary
        double tAreaP = this.TowerP.GetArea();
        double theightP = Math.Floor(tArea * ratio / tAreaP);
        // be attention of the height, +1 is needed
        Brep exP = this.TowerP.Faces[0].CreateExtrusion(new Line(0, 0, 0, 0, 0, theightP + 1).ToNurbsCurve(), true);
        exP.Transform(moveZ);
        this.TowerPStructure = exP;
      }
      if (this.TowerS != null) {
        // extrude and move - secondary
        double tAreaS = this.TowerS.GetArea();
        double theightS = Math.Floor(tArea * (1 - ratio) / tAreaS);
        Brep exS = this.TowerS.Faces[0].CreateExtrusion(new Line(0, 0, 0, 0, 0, theightS + 1).ToNurbsCurve(), true);
        exS.Transform(moveZ);
        this.TowerSStructure = exS;
      }
    }
  }
  // </Custom additional code> 

  private List<string> __err = new List<string>(); //Do not modify this list directly.
  private List<string> __out = new List<string>(); //Do not modify this list directly.
  private RhinoDoc doc = Instances.ActiveRhinoDoc;       //Legacy field.
  private IGH_ActiveObject owner;                  //Legacy field.
  private int runCount;                            //Legacy field.
  
  public override void InvokeRunScript(IGH_Component owner, object rhinoDocument, int iteration, List<object> inputs, IGH_DataAccess DA)
  {
    //Prepare for a new run...
    //1. Reset lists
    this.__out.Clear();
    this.__err.Clear();

    this.Component = owner;
    this.Iteration = iteration;
    this.GrasshopperDocument = owner.OnPingDocument();
    this.RhinoDocument = rhinoDocument as Rhino.RhinoDoc;

    this.owner = this.Component;
    this.runCount = this.Iteration;
    this. doc = this.RhinoDocument;

    //2. Assign input parameters
        List<Surface> Blocks = null;
    if (inputs[0] != null)
    {
      Blocks = GH_DirtyCaster.CastToList<Surface>(inputs[0]);
    }
    List<double> FAR = null;
    if (inputs[1] != null)
    {
      FAR = GH_DirtyCaster.CastToList<double>(inputs[1]);
    }
    double BSR1 = default(double);
    if (inputs[2] != null)
    {
      BSR1 = (double)(inputs[2]);
    }

    double BSR2 = default(double);
    if (inputs[3] != null)
    {
      BSR2 = (double)(inputs[3]);
    }

    double BSR3 = default(double);
    if (inputs[4] != null)
    {
      BSR3 = (double)(inputs[4]);
    }

    List<Curve> RdA = null;
    if (inputs[5] != null)
    {
      RdA = GH_DirtyCaster.CastToList<Curve>(inputs[5]);
    }
    List<Curve> RdB = null;
    if (inputs[6] != null)
    {
      RdB = GH_DirtyCaster.CastToList<Curve>(inputs[6]);
    }
    double TRatio = default(double);
    if (inputs[7] != null)
    {
      TRatio = (double)(inputs[7]);
    }



    //3. Declare output parameters
      object TowerP = null;
  object TowerS = null;
  object Outline = null;
  object Buildings = null;
  object Yard = null;


    //4. Invoke RunScript
    RunScript(Blocks, FAR, BSR1, BSR2, BSR3, RdA, RdB, TRatio, ref TowerP, ref TowerS, ref Outline, ref Buildings, ref Yard);
      
    try
    {
      //5. Assign output parameters to component...
            if (TowerP != null)
      {
        if (GH_Format.TreatAsCollection(TowerP))
        {
          IEnumerable __enum_TowerP = (IEnumerable)(TowerP);
          DA.SetDataList(1, __enum_TowerP);
        }
        else
        {
          if (TowerP is Grasshopper.Kernel.Data.IGH_DataTree)
          {
            //merge tree
            DA.SetDataTree(1, (Grasshopper.Kernel.Data.IGH_DataTree)(TowerP));
          }
          else
          {
            //assign direct
            DA.SetData(1, TowerP);
          }
        }
      }
      else
      {
        DA.SetData(1, null);
      }
      if (TowerS != null)
      {
        if (GH_Format.TreatAsCollection(TowerS))
        {
          IEnumerable __enum_TowerS = (IEnumerable)(TowerS);
          DA.SetDataList(2, __enum_TowerS);
        }
        else
        {
          if (TowerS is Grasshopper.Kernel.Data.IGH_DataTree)
          {
            //merge tree
            DA.SetDataTree(2, (Grasshopper.Kernel.Data.IGH_DataTree)(TowerS));
          }
          else
          {
            //assign direct
            DA.SetData(2, TowerS);
          }
        }
      }
      else
      {
        DA.SetData(2, null);
      }
      if (Outline != null)
      {
        if (GH_Format.TreatAsCollection(Outline))
        {
          IEnumerable __enum_Outline = (IEnumerable)(Outline);
          DA.SetDataList(3, __enum_Outline);
        }
        else
        {
          if (Outline is Grasshopper.Kernel.Data.IGH_DataTree)
          {
            //merge tree
            DA.SetDataTree(3, (Grasshopper.Kernel.Data.IGH_DataTree)(Outline));
          }
          else
          {
            //assign direct
            DA.SetData(3, Outline);
          }
        }
      }
      else
      {
        DA.SetData(3, null);
      }
      if (Buildings != null)
      {
        if (GH_Format.TreatAsCollection(Buildings))
        {
          IEnumerable __enum_Buildings = (IEnumerable)(Buildings);
          DA.SetDataList(4, __enum_Buildings);
        }
        else
        {
          if (Buildings is Grasshopper.Kernel.Data.IGH_DataTree)
          {
            //merge tree
            DA.SetDataTree(4, (Grasshopper.Kernel.Data.IGH_DataTree)(Buildings));
          }
          else
          {
            //assign direct
            DA.SetData(4, Buildings);
          }
        }
      }
      else
      {
        DA.SetData(4, null);
      }
      if (Yard != null)
      {
        if (GH_Format.TreatAsCollection(Yard))
        {
          IEnumerable __enum_Yard = (IEnumerable)(Yard);
          DA.SetDataList(5, __enum_Yard);
        }
        else
        {
          if (Yard is Grasshopper.Kernel.Data.IGH_DataTree)
          {
            //merge tree
            DA.SetDataTree(5, (Grasshopper.Kernel.Data.IGH_DataTree)(Yard));
          }
          else
          {
            //assign direct
            DA.SetData(5, Yard);
          }
        }
      }
      else
      {
        DA.SetData(5, null);
      }

    }
    catch (Exception ex)
    {
      this.__err.Add(string.Format("Script exception: {0}", ex.Message));
    }
    finally
    {
      //Add errors and messages... 
      if (owner.Params.Output.Count > 0)
      {
        if (owner.Params.Output[0] is Grasshopper.Kernel.Parameters.Param_String)
        {
          List<string> __errors_plus_messages = new List<string>();
          if (this.__err != null) { __errors_plus_messages.AddRange(this.__err); }
          if (this.__out != null) { __errors_plus_messages.AddRange(this.__out); }
          if (__errors_plus_messages.Count > 0) 
            DA.SetDataList(0, __errors_plus_messages);
        }
      }
    }
  }
}
