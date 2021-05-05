﻿using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Speckle.Core.Kits;
using Speckle.Core.Models;
using BE = Objects.BuiltElements;
using BER = Objects.BuiltElements.Revit;
using BERC = Objects.BuiltElements.Revit.Curve;
using DB = Autodesk.Revit.DB;

namespace Objects.Converter.Revit
{
  public partial class ConverterRevit : ISpeckleConverter
  {
#if REVIT2023
    public static string RevitAppName = Applications.Revit2023;
#elif REVIT2022
    public static string RevitAppName = Applications.Revit2022;
#elif REVIT2021
    public static string RevitAppName = Applications.Revit2021;
#elif REVIT2020
    public static string RevitAppName = Applications.Revit2020;
#else
    public static string RevitAppName = Applications.Revit2019;
#endif

    #region ISpeckleConverter props

    public string Description => "Default Speckle Kit for Revit";
    public string Name => nameof(ConverterRevit);
    public string Author => "Speckle";
    public string WebsiteOrEmail => "https://speckle.systems";

    public IEnumerable<string> GetServicedApplications() => new string[] { RevitAppName };

    #endregion ISpeckleConverter props

    public Document Doc { get; private set; }

    /// <summary>
    /// <para>To know which other objects are being converted, in order to sort relationships between them.
    /// For example, elements that have children use this to determine whether they should send their children out or not.</para>
    /// </summary>
    public List<ApplicationPlaceholderObject> ContextObjects { get; set; } = new List<ApplicationPlaceholderObject>();

    /// <summary>
    /// <para>To keep track of previously received objects from a given stream in here. If possible, conversions routines
    /// will edit an existing object, otherwise they will delete the old one and create the new one.</para>
    /// </summary>
    public List<ApplicationPlaceholderObject> PreviousContextObjects { get; set; } = new List<ApplicationPlaceholderObject>();

    /// <summary>
    /// Keeps track of the current host element that is creating any sub-objects it may have.
    /// </summary>
    public HostObject CurrentHostElement { get; set; }

    /// <summary>
    /// Used when sending; keeps track of all the converted objects so far. Child elements first check in here if they should convert themselves again (they may have been converted as part of a parent's hosted elements).
    /// </summary>
    public List<string> ConvertedObjectsList { get; set; } = new List<string>();

    public HashSet<Exception> ConversionErrors { get; private set; } = new HashSet<Exception>();

    public Dictionary<string, BE.Level> Levels { get; private set; } = new Dictionary<string, BE.Level>();

    public ConverterRevit() { }

    public void SetContextDocument(object doc) => Doc = (Document)doc;

    public void SetContextObjects(List<ApplicationPlaceholderObject> objects) => ContextObjects = objects;
    public void SetPreviousContextObjects(List<ApplicationPlaceholderObject> objects) => PreviousContextObjects = objects;

    public Base ConvertToSpeckle(object @object)
    {
      Base returnObject = null;
      switch (@object)
      {
        case DB.DetailCurve o:
          returnObject = DetailCurveToSpeckle(o);
          break;
        case DB.DirectShape o:
          returnObject = DirectShapeToSpeckle(o);
          break;
        case DB.FamilyInstance o:
          returnObject = FamilyInstanceToSpeckle(o);
          break;
        case DB.Floor o:
          returnObject = FloorToSpeckle(o);
          break;
        case DB.Level o:
          returnObject = LevelToSpeckle(o);
          break;
        case DB.View o:
          returnObject = ViewToSpeckle(o);
          break;
        case DB.ModelCurve o:

          if ((BuiltInCategory)o.Category.Id.IntegerValue == BuiltInCategory.OST_RoomSeparationLines)
          {
            returnObject = RoomBoundaryLineToSpeckle(o);
          }
          else
          {
            returnObject = ModelCurveToSpeckle(o);
          }

          break;
        case DB.Opening o:
          returnObject = OpeningToSpeckle(o);
          break;
        case DB.RoofBase o:
          returnObject = RoofToSpeckle(o);
          break;
        case DB.Architecture.Room o:
          returnObject = RoomToSpeckle(o);
          break;
        case DB.Architecture.TopographySurface o:
          returnObject = TopographyToSpeckle(o);
          break;
        case DB.Wall o:
          returnObject = WallToSpeckle(o);
          break;
        case DB.Mechanical.Duct o:
          returnObject = DuctToSpeckle(o);
          break;
        case DB.Plumbing.Pipe o:
          returnObject = PipeToSpeckle(o);
          break;
        case DB.Electrical.Wire o:
          returnObject = WireToSpeckle(o);
          break;
        //these should be handled by curtain walls
        case DB.CurtainGridLine _:
          returnObject = null;
          break;
        case DB.Architecture.BuildingPad o:
          returnObject = BuildingPadToSpeckle(o);
          break;
        case DB.Architecture.Stairs o:
          returnObject = StairToSpeckle(o);
          break;
        //these are handled by Stairs
        case DB.Architecture.StairsRun _:
          returnObject = null;
          break;
        case DB.Architecture.StairsLanding _:
          returnObject = null;
          break;
        case DB.Architecture.Railing o:
          returnObject = RailingToSpeckle(o);
          break;
        case DB.Architecture.TopRail _:
          returnObject = null;
          break;
        case DB.Ceiling o:
          returnObject = CeilingToSpeckle(o);
          break;
        case DB.ProjectInfo o:
          returnObject = ProjectInfoToSpeckle(o);
          break;
        case DB.ElementType o:
          returnObject = ElementTypeToSpeckle(o);
          break;
        default:
          // if we don't have a direct conversion, still try to send this element as a generic RevitElement
          if ((@object as Element).IsElementSupported())
          {
            returnObject = RevitElementToSpeckle(@object as Element);
            break;
          }

          ConversionErrors.Add(new Exception($"Skipping not supported type: {@object.GetType()}{GetElemInfo(@object)}"));
          returnObject = null;
          break;
      }

      // NOTE: Only try generic method assignment if there is no existing render material from conversions;
      // we might want to try later on to capture it more intelligently from inside conversion routines.
      if (returnObject != null && returnObject["renderMaterial"] == null)
      {
        var material = GetElementRenderMaterial(@object as DB.Element);
        returnObject["renderMaterial"] = material;
      }

      return returnObject;
    }

    private string GetElemInfo(object o)
    {
      if (o is Element e)
      {
        return $", name: {e.Name}, id: {e.UniqueId}";
      }

      return "";
    }

    public object ConvertToNative(Base @object)
    {
      // schema check
      var speckleSchema = @object["@SpeckleSchema"] as Base;
      if (speckleSchema != null)
      {
        // find self referential prop and set value to @object if it is null (happens when sent from gh)
        if (CanConvertToNative(speckleSchema))
        {
          var prop = speckleSchema.GetInstanceMembers().Where(o => speckleSchema[o.Name] == null)?.Where(o => o.PropertyType.IsAssignableFrom(@object.GetType()))?.FirstOrDefault();
          if (prop != null)
            speckleSchema[prop.Name] = @object;
          @object = speckleSchema;
        }
      }

      switch (@object)
      {
        //geometry
        case ICurve o:
          return ModelCurveToNative(o);

        case Geometry.Brep o:
          return DirectShapeToNative(o);

        case Geometry.Mesh o:
          return DirectShapeToNative(o);

        //built elems
        case BER.AdaptiveComponent o:
          return AdaptiveComponentToNative(o);

        case BE.Beam o:
          return BeamToNative(o);

        case BE.Brace o:
          return BraceToNative(o);

        case BE.Column o:
          return ColumnToNative(o);

        case BERC.DetailCurve o:
          return DetailCurveToNative(o);

        case BER.DirectShape o:
          return DirectShapeToNative(o);

        case BER.FreeformElement o:
          return FreeformElementToNative(o);

        case BER.FamilyInstance o:
          return FamilyInstanceToNative(o);

        case BE.Floor o:
          return FloorToNative(o);

        case BE.Level o:
          return LevelToNative(o);

        case BERC.ModelCurve o:
          return ModelCurveToNative(o);

        case BE.Opening o:
          return OpeningToNative(o);

        case BERC.RoomBoundaryLine o:
          return RoomBoundaryLineToNative(o);

        case BE.Roof o:
          return RoofToNative(o);

        case BE.Topography o:
          return TopographyToNative(o);

        case BER.RevitFaceWall o:
          return FaceWallToNative(o);

        case BE.Wall o:
          return WallToNative(o);

        case BE.Duct o:
          return DuctToNative(o);

        case BE.Pipe o:
          return PipeToNative(o);

        case BE.Wire o:
          return WireToNative(o);

        case BE.Revit.RevitRailing o:
          return RailingToNative(o);

        case BER.ParameterUpdater o:
          UpdateParameter(o);
          return null;

        case BE.View3D o:
          return ViewToNative(o);

        // other
        case Other.BlockInstance o:
          return BlockInstanceToNative(o);

        default:
          return null;
      }
    }

    public List<Base> ConvertToSpeckle(List<object> objects) => objects.Select(ConvertToSpeckle).ToList();

    public List<object> ConvertToNative(List<Base> objects) => objects.Select(ConvertToNative).ToList();

    public bool CanConvertToSpeckle(object @object)
    {
      return @object
      switch
      {
        DB.DetailCurve _ => true,
        DB.DirectShape _ => true,
        DB.FamilyInstance _ => true,
        DB.Floor _ => true,
        DB.Level _ => true,
        DB.View _ => true,
        DB.ModelCurve _ => true,
        DB.Opening _ => true,
        DB.RoofBase _ => true,
        DB.Architecture.Room _ => true,
        DB.Architecture.TopographySurface _ => true,
        DB.Wall _ => true,
        DB.Mechanical.Duct _ => true,
        DB.Plumbing.Pipe _ => true,
        DB.Electrical.Wire _ => true,
        DB.CurtainGridLine _ => true, //these should be handled by curtain walls
        DB.Architecture.BuildingPad _ => true,
        DB.Architecture.Stairs _ => true,
        DB.Architecture.StairsRun _ => true,
        DB.Architecture.StairsLanding _ => true,
        DB.Architecture.Railing _ => true,
        DB.Architecture.TopRail _ => true,
        DB.Ceiling _ => true,
        DB.Group _ => true,
        DB.ProjectInfo _ => true,
        DB.ElementType _ => true,
        _ => (@object as Element).IsElementSupported()
      };
    }

    public bool CanConvertToNative(Base @object)
    {

      var schema = @object["@SpeckleSchema"] as Base; // check for contained schema
      if (schema != null)
        return CanConvertToNative(schema);

      return @object
      switch
      {
        //geometry
        ICurve _ => true,
        Geometry.Brep _ => true,
        Geometry.Mesh _ => true,
        //built elems
        BER.AdaptiveComponent _ => true,
        BE.Beam _ => true,
        BE.Brace _ => true,
        BE.Column _ => true,
        BERC.DetailCurve _ => true,
        BER.DirectShape _ => true,
        BER.FreeformElement _ => true,
        BER.FamilyInstance _ => true,
        BE.Floor _ => true,
        BE.Level _ => true,
        BERC.ModelCurve _ => true,
        BE.Opening _ => true,
        BERC.RoomBoundaryLine _ => true,
        BE.Roof _ => true,
        BE.Topography _ => true,
        BER.RevitFaceWall _ => true,
        BE.Wall _ => true,
        BE.Duct _ => true,
        BE.Pipe _ => true,
        BE.Wire _ => true,
        BE.Revit.RevitRailing _ => true,
        BER.ParameterUpdater _ => true,
        BE.View3D _ => true,
        Other.BlockInstance _ => true,
        _ => false

      };
    }
  }
}
