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
  private void RunScript(List<Brep> Blocks, Curve Boundary, List<Line> RdA, double HalfWA, List<Line> RdB, double HalfWB, List<Line> RdC, double HalfWC, double PedOff, ref object BuildingSetback, ref object PedestrianOutline)
  {
        // initialize
    List<Block> blocks = new List<Block>();
    foreach (Brep brep in Blocks) {
      Block block = new Block(brep);
      blocks.Add(block);
    }

    Curve[] boundoffset = Boundary.Offset(Plane.WorldXY, HalfWA, inter_tol, CurveOffsetCornerStyle.Sharp);
    Curve[] n_boundoffset = Boundary.Offset(Plane.WorldXY, -HalfWA, inter_tol, CurveOffsetCornerStyle.Sharp);

    // each block, each edge, check the location
    foreach (Block block in blocks) {
      List<Curve> cutter = block.offsetEdges(RdA, RdB, HalfWA, HalfWB, HalfWC, PedOff);
      cutter.Add(boundoffset[0]);
      cutter.Add(n_boundoffset[0]);
      block.cutBrep(cutter);
    }
    
    // pedestrian
    List<Curve> peds = new List<Curve>();
    foreach (Block block in blocks) {
      Curve[] outline = Curve.JoinCurves(block.Brp.Edges);
      Curve[] ped = outline[0].Offset(block.Center, Vector3d.ZAxis, -PedOff, inter_tol, CurveOffsetCornerStyle.Sharp);
      peds.Add(ped[0]);
    }

    // out
    List<Brep> outs = new List<Brep>();
    foreach (Block block in blocks) {
      outs.Add(block.Brp);
    }

    BuildingSetback = outs;
    PedestrianOutline = peds;
  }

  // <Custom additional code> 
    public const double inter_tol = 0.001;
  public const double extent = 10;
  // class block
  public class Block
  {
    // initialize
    public Brep Brp;
    public Point3d Center;

    // constructor
    public Block (Brep brp)
    {
      this.Brp = brp;
      AreaMassProperties amp = AreaMassProperties.Compute(brp);
      this.Center = amp.Centroid;
    }

    // offset the edge
    public List<Curve> offsetEdges (List<Line> RdA, List<Line> RdB, double wa, double wb, double wc, double ped)
    {
      List<Curve> offseted = new List<Curve>();
      foreach (BrepEdge edge in this.Brp.Edges) {
        // if on rda
        if (Block.offsetValue(RdA, edge)) {
          // offset wa
          double w = wa + ped;
          Curve[] offset = edge.ToNurbsCurve().Offset(Plane.WorldXY, w, inter_tol, CurveOffsetCornerStyle.Sharp);
          Curve[] noffset = edge.ToNurbsCurve().Offset(Plane.WorldXY, -w, inter_tol, CurveOffsetCornerStyle.Sharp);
          Line tmp = new Line(offset[0].PointAtStart, offset[0].PointAtEnd);
          Line n_tmp = new Line(noffset[0].PointAtStart, noffset[0].PointAtEnd);
          tmp.Extend(extent, extent);
          n_tmp.Extend(extent, extent);
          offseted.Add(tmp.ToNurbsCurve());
          offseted.Add(n_tmp.ToNurbsCurve());
        }
        else if (Block.offsetValue(RdB, edge)) {
          // on rdb, offset wb
          double w = wb + ped;
          Curve[] offset = edge.ToNurbsCurve().Offset(Plane.WorldXY, w, inter_tol, CurveOffsetCornerStyle.Sharp);
          Curve[] noffset = edge.ToNurbsCurve().Offset(Plane.WorldXY, -w, inter_tol, CurveOffsetCornerStyle.Sharp);
          Line tmp = new Line(offset[0].PointAtStart, offset[0].PointAtEnd);
          Line n_tmp = new Line(noffset[0].PointAtStart, noffset[0].PointAtEnd);
          tmp.Extend(extent, extent);
          n_tmp.Extend(extent, extent);
          offseted.Add(tmp.ToNurbsCurve());
          offseted.Add(n_tmp.ToNurbsCurve());
        }
        else {
          // offset wc
          double w = wc + ped;
          Curve[] offset = edge.ToNurbsCurve().Offset(Plane.WorldXY, w, inter_tol, CurveOffsetCornerStyle.Sharp);
          Curve[] noffset = edge.ToNurbsCurve().Offset(Plane.WorldXY, -w, inter_tol, CurveOffsetCornerStyle.Sharp);
          Line tmp = new Line(offset[0].PointAtStart, offset[0].PointAtEnd);
          Line n_tmp = new Line(noffset[0].PointAtStart, noffset[0].PointAtEnd);
          tmp.Extend(extent, extent);
          n_tmp.Extend(extent, extent);
          offseted.Add(tmp.ToNurbsCurve());
          offseted.Add(n_tmp.ToNurbsCurve());
        }
      }
      return offseted;
    }

    // check the location of edges, maxdistance to rd == 0 -> on that class road
    public static bool offsetValue(List<Line> a, BrepEdge edge)
    {
      foreach (Line rd in a) {
        if (rd.DistanceTo(edge.PointAtStart, true) < inter_tol && rd.DistanceTo(edge.PointAtEnd, true) < inter_tol) {
          // on this road
          return true;
        }
      }
      // none
      return false;
    }

    // divide and get the area largest
    public void cutBrep (List<Curve> cutter)
    {
      Brep[] cuts = this.Brp.Split(cutter, inter_tol);
      cuts = cuts.Where(brep => brep != null).ToArray();
      List<double> areas = new List<double>();
      List<Brep> breps = new List<Brep>();
      foreach (Brep brep in cuts) {
        AreaMassProperties am = AreaMassProperties.Compute(brep);
        areas.Add(am.Area);
        breps.Add(brep);
      }
      this.Brp = breps[areas.IndexOf(areas.Max())];
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
        List<Brep> Blocks = null;
    if (inputs[0] != null)
    {
      Blocks = GH_DirtyCaster.CastToList<Brep>(inputs[0]);
    }
    Curve Boundary = default(Curve);
    if (inputs[1] != null)
    {
      Boundary = (Curve)(inputs[1]);
    }

    List<Line> RdA = null;
    if (inputs[2] != null)
    {
      RdA = GH_DirtyCaster.CastToList<Line>(inputs[2]);
    }
    double HalfWA = default(double);
    if (inputs[3] != null)
    {
      HalfWA = (double)(inputs[3]);
    }

    List<Line> RdB = null;
    if (inputs[4] != null)
    {
      RdB = GH_DirtyCaster.CastToList<Line>(inputs[4]);
    }
    double HalfWB = default(double);
    if (inputs[5] != null)
    {
      HalfWB = (double)(inputs[5]);
    }

    List<Line> RdC = null;
    if (inputs[6] != null)
    {
      RdC = GH_DirtyCaster.CastToList<Line>(inputs[6]);
    }
    double HalfWC = default(double);
    if (inputs[7] != null)
    {
      HalfWC = (double)(inputs[7]);
    }

    double PedOff = default(double);
    if (inputs[8] != null)
    {
      PedOff = (double)(inputs[8]);
    }



    //3. Declare output parameters
      object BuildingSetback = null;
  object PedestrianOutline = null;


    //4. Invoke RunScript
    RunScript(Blocks, Boundary, RdA, HalfWA, RdB, HalfWB, RdC, HalfWC, PedOff, ref BuildingSetback, ref PedestrianOutline);
      
    try
    {
      //5. Assign output parameters to component...
            if (BuildingSetback != null)
      {
        if (GH_Format.TreatAsCollection(BuildingSetback))
        {
          IEnumerable __enum_BuildingSetback = (IEnumerable)(BuildingSetback);
          DA.SetDataList(1, __enum_BuildingSetback);
        }
        else
        {
          if (BuildingSetback is Grasshopper.Kernel.Data.IGH_DataTree)
          {
            //merge tree
            DA.SetDataTree(1, (Grasshopper.Kernel.Data.IGH_DataTree)(BuildingSetback));
          }
          else
          {
            //assign direct
            DA.SetData(1, BuildingSetback);
          }
        }
      }
      else
      {
        DA.SetData(1, null);
      }
      if (PedestrianOutline != null)
      {
        if (GH_Format.TreatAsCollection(PedestrianOutline))
        {
          IEnumerable __enum_PedestrianOutline = (IEnumerable)(PedestrianOutline);
          DA.SetDataList(2, __enum_PedestrianOutline);
        }
        else
        {
          if (PedestrianOutline is Grasshopper.Kernel.Data.IGH_DataTree)
          {
            //merge tree
            DA.SetDataTree(2, (Grasshopper.Kernel.Data.IGH_DataTree)(PedestrianOutline));
          }
          else
          {
            //assign direct
            DA.SetData(2, PedestrianOutline);
          }
        }
      }
      else
      {
        DA.SetData(2, null);
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
