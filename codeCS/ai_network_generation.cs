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
  private void RunScript(Brep Bnd, List<Curve> RdA, int Crt1, double AmpC1, int Dist1, int Crt2, double AmpC2, int Dist2, double RndPara, int CtnRng1, int CtnRng2, int LenRat, int Sd, ref object BLOCKS, ref object RDA, ref object RDB, ref object RDC)
  {
        // initialize
    Random rand = new Random(Sd);
    List<Curve> RdB = new List<Curve>();
    List<Curve> RdC = new List<Curve>();
    List<Block> blocks = new List<Block>();
    Block city = new Block(Bnd);
    // divide the city by Road A, get a list of blocks
    Brep[] initbreps = Block.executeDivision(city, RdA);
    foreach (Brep initbrep in initbreps) {
      blocks.Add(new Block(initbrep));
    }

    // divide all, again and again, until all area is smaller than Crt2
    // calc the distance to Road A, determine factors to Crt1
    // update a new division function
    while (Block.checkToDivision(blocks, Crt1, RdA, Dist1, AmpC1)) {
      List<Block> tmp0 = new List<Block>(blocks);
      foreach (Block block in tmp0) {
        // check by length first
        if (block.checkByLength(LenRat).IsValid) {
          // true, means need a division first
          List<Curve> division = block.divisionAtLongest(rand, RndPara);
          RdB.AddRange(division);
          
          Brep[] dividedbrep = Block.executeDivision(block, division);
          blocks.Remove(block);
          foreach (Brep brep in dividedbrep) {
            blocks.Add(new Block(brep));
          }
        }
      }
      // do check division again
      if (Block.checkToDivision(blocks, Crt1, RdA, Dist1, AmpC1)) {
        // do the division
        List<Block> tmp = new List<Block>(blocks);
        foreach (Block block in tmp) {
          if (block.Area > Crt1 * block.isNearToRdA(RdA, Dist1, AmpC1)) {
            List<Curve> division = block.setDivisionB(rand, CtnRng1, RndPara);
            RdB.AddRange(division);

            Brep[] dividedbrep = Block.executeDivision(block, division);
            blocks.Remove(block);
            foreach (Brep brep in dividedbrep) {
              blocks.Add(new Block(brep));
            }
          }
        }
      }
    }

    // divide until all smaller to the last step, Crt3
    // calc the distance to Road A, determine factors to Crt2
    // determine the division pcs, divide on longer egdes
    while (Block.checkToDivision(blocks, Crt2, RdA, Dist2, AmpC2)) {
      List<Block> tmp0 = new List<Block>(blocks);
      foreach (Block block in tmp0) {
        // check by length first
        if (block.checkByLength(LenRat).IsValid) {
          // true, means need a division first
          List<Curve> division = block.divisionAtLongest(rand, RndPara);
          RdC.AddRange(division);
          
          Brep[] dividedbrep = Block.executeDivision(block, division);
          blocks.Remove(block);
          foreach (Brep brep in dividedbrep) {
            blocks.Add(new Block(brep));
          }
        }
      }
      if (Block.checkToDivision(blocks, Crt2, RdA, Dist2, AmpC2)) {
        // do the division
        List<Block> tmp = new List<Block>(blocks);
        foreach (Block block in tmp) {
          if (block.Area > Crt2 * block.isNearToRdA(RdA, Dist2, AmpC2)) {
            List<Curve> division = block.setDivisionC(RdA, Dist2, AmpC2, rand, CtnRng2, RndPara);
            RdC.AddRange(division);

            Brep[] dividedbrep = Block.executeDivision(block, division);
            blocks.Remove(block);
            foreach (Brep brep in dividedbrep) {
              blocks.Add(new Block(brep));
            }
          }
        }
      }
    }
    
    // output
    List<Brep> outs = new List<Brep>();
    foreach (Block block in blocks) {
      outs.Add(block.Brp);
    }

    BLOCKS = outs;
    RDA = RdA;
    RDB = RdB;
    RDC = RdC;
  }

  // <Custom additional code> 
    public const double inter_tol = 0.001;
  public class Block
  {
    // initialize
    public Brep Brp;
    // public List<Point3d> Ends;
    public Point3d Center;
    public double Area;

    // constructor
    public Block (Brep brep)
    {
      this.Brp = brep;
      // List<Point3d> ends = new List<Point3d>();
      // var vertices = brep.Vertices;
      // foreach (BrepVertex bv in vertices) {
      //   ends.Add(bv.Location);
      // }
      // this.Ends = ends;

      // center
      AreaMassProperties ctn_area = AreaMassProperties.Compute(brep.DuplicateBrep());
      this.Center = ctn_area.Centroid;
      // area
      this.Area = ctn_area.Area;
    }

    // ***basic divisions**
    // get the edge pairs across, build division lines, used in class B
    public List<Curve> divisionByPairs (Random rand, double pararng)
    {
      List<Line> edges = new List<Line>();
      foreach (BrepEdge edge in this.Brp.Edges) {
        edges.Add(new Line(edge.PointAtStart, edge.PointAtEnd));
      }
      int usedid = 0;
      for (int i = 1; i < 4; i++) {
        if (edges[0].MinimumDistanceTo(edges[i]) > inter_tol) {
          usedid = i;
          break;
        }
      }
      Line div1 = new Line(edges[0].PointAt(rand.NextDouble() * pararng * 2 + 0.5 - pararng), edges[usedid].PointAt(rand.NextDouble() * pararng * 2 + 0.5 - pararng));
      List<Line> rest = edges.Where(edge => edges.IndexOf(edge) != 0 && edges.IndexOf(edge) != usedid).ToList();
      Line div2 = new Line(rest[0].PointAt(rand.NextDouble() * pararng * 2 + 0.5 - pararng), rest[1].PointAt(rand.NextDouble() * pararng * 2 + 0.5 - pararng));
      return new List<Curve>() {div1.ToNurbsCurve(), div2.ToNurbsCurve()};
    }

    // normal division, from center to all edges, used in class B and C
    public List<Curve> divisionFromCenter (Random rand, int cntrng, double pararng)
    {
      List<Curve> division = new List<Curve>();
      // get a random point within a range from the center, use as the divide starting point to each edge
      Point3d divcent = new Point3d(this.Center.X + rand.Next(-cntrng, cntrng), this.Center.Y + rand.Next(-cntrng, cntrng), 0);
      // each edge, get a point within a certain range from the midpoint
      foreach (BrepEdge edge in this.Brp.Edges) {
        Curve edgecrv = edge.ToNurbsCurve();
        edgecrv.Domain = new Interval(0, 1);
        // a point on edge, (0.5-pararng,0.5+pararng)
        Point3d divpt = edgecrv.PointAt(rand.NextDouble() * pararng * 2 + 0.5 - pararng);
        Line divln = new Line(divcent, divpt);
        division.Add(divln.ToNurbsCurve());
      }
      return division;
    }

    // get the pair division, and eliminate one segments, used in class C
    public List<Curve> divisionByPairsEliminated (Random rand, int cntrng, double pararng)
    {
      // find division by pairs
      List<Line> edges = new List<Line>();
      foreach (BrepEdge edge in this.Brp.Edges) {
        edges.Add(new Line(edge.PointAtStart, edge.PointAtEnd));
      }
      int usedid = 0;
      for (int i = 1; i < 4; i++) {
        if (edges[0].MinimumDistanceTo(edges[i]) > inter_tol) {
          usedid = i;
          break;
        }
      }
      List<Point3d> pts = new List<Point3d>();
      for (int i = 0; i < 4; i++) {
        pts.Add(edges[i].PointAt(rand.NextDouble() * pararng * 2 + 0.5 - pararng));
      }
      Line div1 = new Line(pts[0], pts[usedid]);
      List<Point3d> rest = pts.Where(pt => pts.IndexOf(pt) != 0 && pts.IndexOf(pt) != usedid).ToList();
      Line div2 = new Line(rest[0], rest[1]);
      // find the longest segment (means in the narrow end of the block)
      double para1, para2;
      Rhino.Geometry.Intersect.Intersection.LineLine(div1, div2, out para1, out para2);
      List<Curve> divisions = new List<Curve>();
      Division dv1 = new Division(div1, para1);
      Division dv2 = new Division(div2, para2);
      if (dv1.MaxSegLen > dv2.MaxSegLen) {
        // eliminate dv1 longer seg
        divisions.Add(dv1.MinSeg.ToNurbsCurve());
        divisions.Add(div2.ToNurbsCurve());
      }
      else {
        divisions.Add(dv2.MinSeg.ToNurbsCurve());
        divisions.Add(div1.ToNurbsCurve());
      }
      return divisions;
    }
    
    // divide once at the edge that is too long
    public List<Curve> divisionAtLongest (Random rand, double pararng)
    {
      // find the longest edge
      List<Line> lns = new List<Line>();
      List<double> lens = new List<double>();
      foreach (BrepEdge edge in this.Brp.Edges) {
        Line tmp = new Line(edge.PointAtStart, edge.PointAtEnd);
        lns.Add(tmp);
        lens.Add(tmp.Length);
      }
      double maxlen = lens.Max();
      Line maxln = lns[lens.IndexOf(maxlen)];
      // find the edge across
      int usedid = 0;
      for (int i = 0; i < lns.Count; i++) {
        if (maxln.MinimumDistanceTo(lns[i]) > inter_tol) {
          usedid = i;
          break;
        }
      }
      Line div = new Line(maxln.PointAt(rand.NextDouble() * pararng * 2 + 0.5 - pararng), lns[usedid].PointAt(rand.NextDouble() * pararng * 2 + 0.5 - pararng));
      return new List<Curve>() {div.ToNurbsCurve()};
    }

    // ***divide blocks in different class***
    // for larger blocks to be divided by Road B, no not use center if the block has 4 edges
    public List<Curve> setDivisionB (Random rand, int cntrng, double pararng)
    {
      if (this.Brp.Edges.Count == 4) {
        return this.divisionByPairs(rand, pararng);
      }
      else {
        return this.divisionFromCenter(rand, cntrng, pararng);
      }
    }

    // get the divide center
    public List<Curve> setDivisionC (List<Curve> RdA, int dist, double amp, Random rand, int cntrng, double pararng)
    {
      // when the block is far, and has 4 edges, eleminate
      //if (this.isNearToRdA(RdA, dist, amp) >= 1 && this.Brp.Edges.Count == 4) {
      //return this.divisionByPairsEliminated(rand, cntrng, pararng);
      //}
      //else {
      return this.divisionFromCenter(rand, cntrng, pararng);
      //}
    }

    // divide the father into sons
    public static Brep[] executeDivision(Block father, List<Curve> division)
    {
      Brep[] sons = father.Brp.Split(division, inter_tol);
      // cull null
      return sons.Where(brep => brep != null).ToArray();
    }

    // ***check division and distance***
    // check if all the blocks is below a area
    public static bool checkToDivision (List<Block> blocks, int area, List<Curve> RdA, int dist, double amp)
    {
      foreach (Block block in blocks) {
        if (block.Area > area * block.isNearToRdA(RdA, dist, amp)) {
          // need division
          return true;
        }
      }
      return false;
    }

    // check the distance to nearest primary road, and dicide the factor to use
    public double isNearToRdA (List<Curve> RdA, int dist, double amp)
    {
      List<double> dists = new List<double>();
      foreach (Curve crv in RdA) {
        Line ln = new Line(crv.PointAtStart, crv.PointAtEnd);
        dists.Add(ln.DistanceTo(this.Center, true));
      }
      if (dists.Min() > dist) {
        // too far, larger factor
        return 1 + amp;
      }
      else {
        // near, smaller factor
        return 1 - amp;
      }
    }

    // ***check the shape, length***
    // check the length, longest shortest
    public Line checkByLength (int rat)
    {
      List<Line> lns = new List<Line>();
      List<double> lens = new List<double>();
      foreach (BrepEdge edge in this.Brp.Edges) {
        Line tmp = new Line(edge.PointAtStart, edge.PointAtEnd);
        lns.Add(tmp);
        lens.Add(tmp.Length);
      }
      double maxlen = lens.Max();
      double minlen = lens.Min();
      if (maxlen / minlen >= rat) {
        return lns[lens.IndexOf(maxlen)];
      }
      else { return new Line(); }
    }
  }

  // define the line with intersections
  public class Division
  {
    public Line Ln;
    public double MaxSegLen;
    public Line MinSeg;

    public Division (Line ln, double para)
    {
      this.Ln = ln;
      Point3d div = this.Ln.PointAt(para);
      List<double> seglen = new List<double>();
      Line seg1 = new Line(div, ln.From);
      Line seg2 = new Line(div, ln.To);
      if (seg1.Length > seg2.Length) {
        this.MaxSegLen = seg1.Length;
        this.MinSeg = seg2;
      }
      else {
        this.MaxSegLen = seg2.Length;
        this.MinSeg = seg1;
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
        Brep Bnd = default(Brep);
    if (inputs[0] != null)
    {
      Bnd = (Brep)(inputs[0]);
    }

    List<Curve> RdA = null;
    if (inputs[1] != null)
    {
      RdA = GH_DirtyCaster.CastToList<Curve>(inputs[1]);
    }
    int Crt1 = default(int);
    if (inputs[2] != null)
    {
      Crt1 = (int)(inputs[2]);
    }

    double AmpC1 = default(double);
    if (inputs[3] != null)
    {
      AmpC1 = (double)(inputs[3]);
    }

    int Dist1 = default(int);
    if (inputs[4] != null)
    {
      Dist1 = (int)(inputs[4]);
    }

    int Crt2 = default(int);
    if (inputs[5] != null)
    {
      Crt2 = (int)(inputs[5]);
    }

    double AmpC2 = default(double);
    if (inputs[6] != null)
    {
      AmpC2 = (double)(inputs[6]);
    }

    int Dist2 = default(int);
    if (inputs[7] != null)
    {
      Dist2 = (int)(inputs[7]);
    }

    double RndPara = default(double);
    if (inputs[8] != null)
    {
      RndPara = (double)(inputs[8]);
    }

    int CtnRng1 = default(int);
    if (inputs[9] != null)
    {
      CtnRng1 = (int)(inputs[9]);
    }

    int CtnRng2 = default(int);
    if (inputs[10] != null)
    {
      CtnRng2 = (int)(inputs[10]);
    }

    int LenRat = default(int);
    if (inputs[11] != null)
    {
      LenRat = (int)(inputs[11]);
    }

    int Sd = default(int);
    if (inputs[12] != null)
    {
      Sd = (int)(inputs[12]);
    }



    //3. Declare output parameters
      object BLOCKS = null;
  object RDA = null;
  object RDB = null;
  object RDC = null;


    //4. Invoke RunScript
    RunScript(Bnd, RdA, Crt1, AmpC1, Dist1, Crt2, AmpC2, Dist2, RndPara, CtnRng1, CtnRng2, LenRat, Sd, ref BLOCKS, ref RDA, ref RDB, ref RDC);
      
    try
    {
      //5. Assign output parameters to component...
            if (BLOCKS != null)
      {
        if (GH_Format.TreatAsCollection(BLOCKS))
        {
          IEnumerable __enum_BLOCKS = (IEnumerable)(BLOCKS);
          DA.SetDataList(1, __enum_BLOCKS);
        }
        else
        {
          if (BLOCKS is Grasshopper.Kernel.Data.IGH_DataTree)
          {
            //merge tree
            DA.SetDataTree(1, (Grasshopper.Kernel.Data.IGH_DataTree)(BLOCKS));
          }
          else
          {
            //assign direct
            DA.SetData(1, BLOCKS);
          }
        }
      }
      else
      {
        DA.SetData(1, null);
      }
      if (RDA != null)
      {
        if (GH_Format.TreatAsCollection(RDA))
        {
          IEnumerable __enum_RDA = (IEnumerable)(RDA);
          DA.SetDataList(2, __enum_RDA);
        }
        else
        {
          if (RDA is Grasshopper.Kernel.Data.IGH_DataTree)
          {
            //merge tree
            DA.SetDataTree(2, (Grasshopper.Kernel.Data.IGH_DataTree)(RDA));
          }
          else
          {
            //assign direct
            DA.SetData(2, RDA);
          }
        }
      }
      else
      {
        DA.SetData(2, null);
      }
      if (RDB != null)
      {
        if (GH_Format.TreatAsCollection(RDB))
        {
          IEnumerable __enum_RDB = (IEnumerable)(RDB);
          DA.SetDataList(3, __enum_RDB);
        }
        else
        {
          if (RDB is Grasshopper.Kernel.Data.IGH_DataTree)
          {
            //merge tree
            DA.SetDataTree(3, (Grasshopper.Kernel.Data.IGH_DataTree)(RDB));
          }
          else
          {
            //assign direct
            DA.SetData(3, RDB);
          }
        }
      }
      else
      {
        DA.SetData(3, null);
      }
      if (RDC != null)
      {
        if (GH_Format.TreatAsCollection(RDC))
        {
          IEnumerable __enum_RDC = (IEnumerable)(RDC);
          DA.SetDataList(4, __enum_RDC);
        }
        else
        {
          if (RDC is Grasshopper.Kernel.Data.IGH_DataTree)
          {
            //merge tree
            DA.SetDataTree(4, (Grasshopper.Kernel.Data.IGH_DataTree)(RDC));
          }
          else
          {
            //assign direct
            DA.SetData(4, RDC);
          }
        }
      }
      else
      {
        DA.SetData(4, null);
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
