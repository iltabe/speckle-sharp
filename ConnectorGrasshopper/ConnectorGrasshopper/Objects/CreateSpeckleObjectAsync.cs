﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ConnectorGrasshopper.Extras;
using Grasshopper.Kernel;
using GrasshopperAsyncComponent;
using Rhino;
using Speckle.Core.Kits;
using Speckle.Core.Logging;
using Speckle.Core.Models;
using Utilities = ConnectorGrasshopper.Extras.Utilities;

namespace ConnectorGrasshopper.Objects
{
  public class CreateSpeckleObjectAsync : SelectKitAsyncComponentBase , IGH_VariableParameterComponent
  {
    protected override Bitmap Icon => Properties.Resources.CreateSpeckleObject;

    public override GH_Exposure Exposure => GH_Exposure.primary;

    public override Guid ComponentGuid => new Guid("FC2EF86F-2C12-4DC2-B216-33BFA409A0FC");
    
    public CreateSpeckleObjectAsync() : base("Create Speckle Object", "CSO",
      "Allows you to create a Speckle object by setting its keys and values.",
      ComponentCategories.PRIMARY_RIBBON, ComponentCategories.OBJECTS)
    {
        
        BaseWorker = new CreateSpeckleObjectWorker(this, Converter);
        Params.ParameterNickNameChanged += (sender, args) =>
        {
          Console.WriteLine("nickname changed!");
          args.Parameter.Name = args.Parameter.NickName;
          ExpireSolution(true);
        };
        Params.ParameterChanged += (sender, args) =>
        {
          if (args.OriginalArguments.Type == GH_ObjectEventType.NickName ||
              args.OriginalArguments.Type == GH_ObjectEventType.NickNameAccepted)
          {
            Console.WriteLine("nickname changed!");
            args.Parameter.Name = args.Parameter.NickName;
            ExpireSolution(true);
          }
        };
    }

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
      // All inputs are dynamically generated by the user.
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
      pManager.AddParameter(new SpeckleBaseParam("Speckle Object", "O", "Created speckle object", GH_ParamAccess.item));
    }

    public bool CanInsertParameter(GH_ParameterSide side, int index) => side == GH_ParameterSide.Input;

    public bool CanRemoveParameter(GH_ParameterSide side, int index) => side == GH_ParameterSide.Input;

    public IGH_Param CreateParameter(GH_ParameterSide side, int index)
    {
      var myParam = new GenericAccessParam
      {
        Name = GH_ComponentParamServer.InventUniqueNickname("ABCD", Params.Input),
        MutableNickName = true,
        Optional = true
      };

      myParam.NickName = myParam.Name;
      myParam.Optional = false;
      myParam.ObjectChanged += (sender, e) => {};
      return myParam;
    }

    public bool DestroyParameter(GH_ParameterSide side, int index)
    {
      return true;
    }

    public void VariableParameterMaintenance()
    {
      Console.WriteLine("parameter maintenance!");
    }
  }

  public class CreateSpeckleObjectWorker : WorkerInstance
  {
    public Base @base;
    public ISpeckleConverter Converter;
    private Dictionary<string, object> inputData;
    public CreateSpeckleObjectWorker(GH_Component parent, ISpeckleConverter converter) : base(parent)
    {
      Converter = converter;
      inputData = new Dictionary<string, object>();
    }

    public override WorkerInstance Duplicate() => new CreateSpeckleObjectWorker(Parent, Converter);

    public override void DoWork(Action<string, double> ReportProgress, Action Done)
    {
      try
      {
        Parent.Message = "Creating...";
        @base = new Base();
        var hasErrors = false;

        inputData.Keys.ToList().ForEach(key =>
        {
          var value = inputData[key];

          
          if (value is List<object> list)
          {
            // Value is a list of items, iterate and convert.
            var converted = list.Select(item => Utilities.TryConvertItemToSpeckle(item, Converter)).ToList();
            try
            {
              @base[key] = converted;
            }
            catch (Exception e)
            {
              Log.CaptureAndThrow(e);
              Parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"{e.Message}");
              Parent.Message = "Error";
              RhinoApp.InvokeOnUiThread(new Action(()=> Parent.OnDisplayExpired(true)));
              hasErrors = true;
            }
          }
          else
          {
            // If value is not list, it is a single item.
            try
            {
              var obj = Utilities.TryConvertItemToSpeckle(value, Converter);
              @base[key] = obj;
            }
            catch (Exception e)
            {
              Log.CaptureException(e);
              Parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"{e.Message}");
              Parent.Message = "Error";
              RhinoApp.InvokeOnUiThread(new Action(()=> Parent.OnDisplayExpired(true)));
              hasErrors = true;
            }        
          }
        });

        if (hasErrors)
        {
          return;
        }
      }
      catch (Exception e)
      {
        // If we reach this, something happened that we weren't expecting...
        Log.CaptureException(e);
        Parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Something went terribly wrong... " + e.Message);
        Parent.Message = "Error";
        return;
      }
      
      // Let's always call done!
      Done();
    }

    public override void SetData(IGH_DataAccess DA)
    {
      DA.SetData(0, new GH_SpeckleBase{ Value = @base });
    }

    public override void GetData(IGH_DataAccess DA, GH_ComponentParamServer Params)
    {
      DA.DisableGapLogic();
      Params.Input.ForEach(ighParam =>
      {
        var param = ighParam as GenericAccessParam;
        var index = Params.IndexOfInputParam(param.Name);
        var detachable = param.Detachable;
        var key = detachable ? "@" + param.NickName : param.NickName;

        switch (param.Access)
        {
          case GH_ParamAccess.item:
            object value = null;
            DA.GetData(index, ref value);
            inputData[key] = value;
            break;
          case GH_ParamAccess.list:
            var values = new List<object>();
            DA.GetDataList(index, values);
            inputData[key] = values;
            break;
          case GH_ParamAccess.tree:
            break;
          default:
            throw new ArgumentOutOfRangeException();
        }
      });
    }
  }
}
