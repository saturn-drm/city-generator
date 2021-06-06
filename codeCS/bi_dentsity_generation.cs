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
  private void RunScript(List<Brep> subS, List<Line> RdA, List<Line> RdB, List<Line> RdC, List<int> ClsA, List<int> ClsB, List<int> ClsC, List<double> Wgt, List<double> HtA, List<double> HtB, List<double> HtC, bool TrgA, bool TrgB, bool TrgC, ref object Block, ref object Density)
  {
        // construct array of block instances
    Block[] blocks = new Block[subS.Count];
    for (int i = 0; i < subS.Count; i++) {
      Block block = new Block(subS[i]); // we have overload here, no worry about brep vs surface
      blocks[i] = block;
    }

    // construct array of roads with Class and Height
    Road[] rda = new Road[RdA.Count];
    Road[] rdb = new Road[RdB.Count];
    Road[] rdc = new Road[RdC.Count];
    // RdA
    if (TrgA) {
      for (int i = 0; i < RdA.Count; i++) {
        Road rd = new Road(RdA[i], ClsA, HtA);
        rda[i] = rd;
      }
    }
    // RdB
    if (TrgB) {
      for (int i = 0; i < RdB.Count; i++) {
        Road rd = new Road(RdB[i], ClsB, HtB);
        rdb[i] = rd;
      }
    }
    // RdC
    if (TrgC) {
      for (int i = 0; i < RdC.Count; i++) {
        Road rd = new Road(RdC[i], ClsC, HtC);
        rdc[i] = rd;
      }
    }

    // calculate and classify
    List<double> heights = new List<double>();
    //List<Brep> breps = new List<Brep>();
    List<Surface> surfaces = new List<Surface>();
    foreach (Block block in blocks) {
      // class A
      if (TrgA) {
        block.HA = block.SetHeight(rda);
      }
      // class B
      if (TrgB) {
        block.HB = block.SetHeight(rdb);
      }
      // class C
      if (TrgC) {
        block.HC = block.SetHeight(rdc);
      }
      // overall
      block.SetAllHeight(Wgt);
      // output
      heights.Add(block.HAll);
      surfaces.Add(block.Srf);
    }

    // output
    Block = surfaces;
    Density = heights;
  }

  // <Custom additional code> 
    // define block
  public class Block
  {
    // initialize
    public Surface Srf;
    public Brep Brp;
    public double HA;
    public double HB;
    public double HC;
    public double HAll;
    public List<Point3d> Ends;

    // constructor FROM BREP
    public Block(Brep brep)
    {
      this.Srf = brep.Faces[0];
      this.Brp = brep;
      this.HA = 0;
      this.HB = 0;
      this.HC = 0;
      List<Point3d> ends = new List<Point3d>();
      var vertices = brep.Vertices;
      foreach (BrepVertex bv in vertices) {
        ends.Add(bv.Location);
      }
      this.Ends = ends;
    }

    // get center
    public Point3d Center()
    {
      int count = 0;
      Point3d sum = new Point3d(0, 0, 0);
      foreach (Point3d pt in this.Ends) {
        sum += pt;
        count++;
      }
      return sum / count;
    }

    // get the min dist from center to all roads of same class
    public double SetHeight (Road[] rds)
    {
      List<double> dists = new List<double>();
      foreach (Road rd in rds) {
        dists.Add(rd.Ln.DistanceTo(this.Center(), false));
      }
      double mindist = dists.Min();
      return Block.DistToHeight(mindist, rds);
    }

    // generate the height
    public static double DistToHeight (double dist, Road[] rds)
    {
      List<int> cls = rds[0].Cls;
      List<double> ht = rds[0].Ht;
      if (dist <= cls[0] && dist > cls[1]) {
        return ht[0];
      }
      else if (dist <= cls[1] && dist > cls[2]) {
        return ht[1];
      }
      else if (dist <= cls[2]) {
        return ht[2];
      }
      else {
        return 0;
      }
    }

    // overall height
    public void SetAllHeight (List<double> wt)
    {
      this.HAll = this.HA * wt[0] + this.HB * wt[1] + this.HC * wt[2];
    }
  }

  // define roads
  public class Road
  {
    // initialize
    public Line Ln;
    public List<int> Cls;
    public List<double> Ht;

    // constructor
    public Road (Line ln, List<int> cls, List<double> ht)
    {
      this.Ln = ln;
      this.Cls = cls;
      this.Ht = ht;
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
        List<Brep> subS = null;
    if (inputs[0] != null)
    {
      subS = GH_DirtyCaster.CastToList<Brep>(inputs[0]);
    }
    List<Line> RdA = null;
    if (inputs[1] != null)
    {
      RdA = GH_DirtyCaster.CastToList<Line>(inputs[1]);
    }
    List<Line> RdB = null;
    if (inputs[2] != null)
    {
      RdB = GH_DirtyCaster.CastToList<Line>(inputs[2]);
    }
    List<Line> RdC = null;
    if (inputs[3] != null)
    {
      RdC = GH_DirtyCaster.CastToList<Line>(inputs[3]);
    }
    List<int> ClsA = null;
    if (inputs[4] != null)
    {
      ClsA = GH_DirtyCaster.CastToList<int>(inputs[4]);
    }
    List<int> ClsB = null;
    if (inputs[5] != null)
    {
      ClsB = GH_DirtyCaster.CastToList<int>(inputs[5]);
    }
    List<int> ClsC = null;
    if (inputs[6] != null)
    {
      ClsC = GH_DirtyCaster.CastToList<int>(inputs[6]);
    }
    List<double> Wgt = null;
    if (inputs[7] != null)
    {
      Wgt = GH_DirtyCaster.CastToList<double>(inputs[7]);
    }
    List<double> HtA = null;
    if (inputs[8] != null)
    {
      HtA = GH_DirtyCaster.CastToList<double>(inputs[8]);
    }
    List<double> HtB = null;
    if (inputs[9] != null)
    {
      HtB = GH_DirtyCaster.CastToList<double>(inputs[9]);
    }
    List<double> HtC = null;
    if (inputs[10] != null)
    {
      HtC = GH_DirtyCaster.CastToList<double>(inputs[10]);
    }
    bool TrgA = default(bool);
    if (inputs[11] != null)
    {
      TrgA = (bool)(inputs[11]);
    }

    bool TrgB = default(bool);
    if (inputs[12] != null)
    {
      TrgB = (bool)(inputs[12]);
    }

    bool TrgC = default(bool);
    if (inputs[13] != null)
    {
      TrgC = (bool)(inputs[13]);
    }



    //3. Declare output parameters
      object Block = null;
  object Density = null;


    //4. Invoke RunScript
    RunScript(subS, RdA, RdB, RdC, ClsA, ClsB, ClsC, Wgt, HtA, HtB, HtC, TrgA, TrgB, TrgC, ref Block, ref Density);
      
    try
    {
      //5. Assign output parameters to component...
            if (Block != null)
      {
        if (GH_Format.TreatAsCollection(Block))
        {
          IEnumerable __enum_Block = (IEnumerable)(Block);
          DA.SetDataList(1, __enum_Block);
        }
        else
        {
          if (Block is Grasshopper.Kernel.Data.IGH_DataTree)
          {
            //merge tree
            DA.SetDataTree(1, (Grasshopper.Kernel.Data.IGH_DataTree)(Block));
          }
          else
          {
            //assign direct
            DA.SetData(1, Block);
          }
        }
      }
      else
      {
        DA.SetData(1, null);
      }
      if (Density != null)
      {
        if (GH_Format.TreatAsCollection(Density))
        {
          IEnumerable __enum_Density = (IEnumerable)(Density);
          DA.SetDataList(2, __enum_Density);
        }
        else
        {
          if (Density is Grasshopper.Kernel.Data.IGH_DataTree)
          {
            //merge tree
            DA.SetDataTree(2, (Grasshopper.Kernel.Data.IGH_DataTree)(Density));
          }
          else
          {
            //assign direct
            DA.SetData(2, Density);
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
