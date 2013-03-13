﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB.Analysis;

namespace Dynamo.Utilities
{
    class dynUtils
    {
        private static ElementId _testid;
        /// <summary>
        /// Utility function to determine if an Element of the given ID exists in the document.
        /// </summary>
        /// <param name="e">ID to check.</param>
        /// <returns>True if exists, false otherwise.</returns>
        public static bool TryGetElement(ElementId id, out Element e)
        {
            try
            {
                e = dynRevitSettings.Doc.Document.GetElement(id);
                if (e != null)
                {
                    _testid = e.Id;
                    return true;
                }
                else
                {
                    e = null;
                    return false;
                }
            }
            catch
            {
                e = null;
                return false;
            }
        }


        /// <summary>
        /// Makes a new generic IEnumerable instance out of a non-generic one.
        /// </summary>
        /// <typeparam name="T">The out-type of the new IEnumerable</typeparam>
        /// <param name="en">Non-generic IEnumerable</param>
        /// <returns></returns>
        public static IEnumerable<T> MakeEnumerable<T>(IEnumerable en)
        {
            foreach (T item in en)
            {
                yield return item;
            }
        }

        /// <summary>
        /// Makes a new generic IEnumerable instance out of a non-generic one.
        /// </summary>
        /// <param name="en">Non-generic IEnumerable</param>
        /// <returns></returns>
        public static IEnumerable<object> MakeEnumerable(IEnumerable en)
        {
            return MakeEnumerable<object>(en);
        }



        /// <summary>
        /// Creates a sketch plane by projecting one point's z coordinate down to the other's z coordinate.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="doc"></param>
        /// <param name="pt1">The start point</param>
        /// <param name="pt2">The end point</param>
        /// <returns></returns>
        public static SketchPlane CreateSketchPlaneForModelCurve(UIApplication app, UIDocument doc,
            XYZ pt1, XYZ pt2)
        {
            XYZ v1, v2, norm;

            if (pt1.X == pt2.X && pt1.Y == pt2.Y)
            {
                //this is a vertical line
                //make the other axis 
                v1 = (pt2 - pt1).Normalize();
                v2 = ((new XYZ(pt1.X, pt1.Y + 1.0, pt1.Z)) - pt1).Normalize();
                norm = v1.CrossProduct(v2);
            }
            else if (Math.Abs(pt2.Z - pt1.Z) > .00000001)
            {
                //flatten in the z direction
                v1 = (pt2 - pt1).Normalize();
                v2 = ((new XYZ(pt2.X, pt2.Y, pt1.Z)) - pt1).Normalize();
                norm = v1.CrossProduct(v2);
            }
            else if (Math.Abs(pt2.Y - pt1.Y) > .00000001)
            {
                //flatten in the y direction
                v1 = (pt2 - pt1).Normalize();
                v2 = ((new XYZ(pt2.X, pt1.Y, pt2.Z)) - pt1).Normalize();
                norm = v1.CrossProduct(v2);
            }
            else
            {
                //flatten in the x direction
                v1 = (pt2 - pt1).Normalize();
                v2 = ((new XYZ(pt1.X, pt2.Y, pt2.Z)) - pt1).Normalize();
                norm = v1.CrossProduct(v2);
            }
            Plane p = app.Application.Create.NewPlane(norm, pt1);

            SketchPlane sp = doc.Document.Create.NewSketchPlane(p);
            return sp;
        }
    }

    public static class dynRevitSettings
    {
        static HashSet<ElementId> userSelectedElements = new HashSet<ElementId>();

        public static Element SpatialFieldManagerUpdated { get; internal set; }
        public static UIApplication Revit { get; internal set; }
        public static UIDocument Doc { get; internal set; }
        public static Level DefaultLevel { get; internal set; }
        public static DynamoWarningSwallower WarningSwallower { get; internal set; }
        public static Transaction MainTransaction { get; internal set; }

        public class DynamoWarningSwallower : IFailuresPreprocessor
        {
            public FailureProcessingResult PreprocessFailures(
                FailuresAccessor a)
            {
                // inside event handler, get all warnings

                IList<FailureMessageAccessor> failures
                    = a.GetFailureMessages();

                foreach (FailureMessageAccessor f in failures)
                {
                    // check failure definition ids
                    // against ones to dismiss:

                    FailureDefinitionId id
                        = f.GetFailureDefinitionId();

                    if (BuiltInFailures.InaccurateFailures.InaccurateLine == id ||
                        BuiltInFailures.OverlapFailures.DuplicateInstances == id ||
                        BuiltInFailures.InaccurateFailures.InaccurateCurveBasedFamily == id ||
                        BuiltInFailures.InaccurateFailures.InaccurateBeamOrBrace == id ||
                        BuiltInFailures.InaccurateFailures.InaccurateLine == id
                        )
                    {
                        a.DeleteWarning(f);
                    }
                    else
                    {
                        a.RollBackPendingTransaction();
                    }

                }
                return FailureProcessingResult.Continue;
            }
        }

        public class SelectionHelper
        {

            //RequestReferencePointSelection
            public static ReferencePoint RequestReferencePointSelection(UIDocument doc, string message)
            {
                try
                {
                    ReferencePoint rp = null;

                    Selection choices = doc.Selection;

                    choices.Elements.Clear();

                    //MessageBox.Show(message);
                    dynSettings.Bench.Log(message);

                    //create some geometry options so that we computer references
                    Autodesk.Revit.DB.Options opts = new Options();
                    opts.ComputeReferences = true;
                    opts.DetailLevel = ViewDetailLevel.Medium;
                    opts.IncludeNonVisibleObjects = false;

                    Reference pointRef = doc.Selection.PickObject(ObjectType.Element);
                    //Reference pointRef = IdlePromise<Reference>.ExecuteOnIdle(
                    //    () => doc.Selection.PickObject(ObjectType.Element)
                    //);


                    if (pointRef != null)
                    {
                        rp = dynRevitSettings.Doc.Document.GetElement(pointRef) as ReferencePoint;
                    }
                    return rp;
                }
                catch (Exception ex)
                {
                    dynSettings.Bench.Log(ex);
                    return null;
                }


            }
            public static CurveElement RequestCurveElementSelection(UIDocument doc, string message)
            {
                try
                {
                    CurveElement c = null;
                    Curve cv = null;

                    Selection choices = doc.Selection;

                    choices.Elements.Clear();

                    //MessageBox.Show(message);
                    dynSettings.Bench.Log(message);

                    Reference curveRef = doc.Selection.PickObject(ObjectType.Element);

                    //c = curveRef.Element as ModelCurve;
                    c = dynRevitSettings.Revit.ActiveUIDocument.Document.GetElement(curveRef) as CurveElement;

                    if (c != null)
                    {
                        cv = c.GeometryCurve;
                    }
                    return c;
                }
                catch (Exception ex)
                {
                    dynSettings.Bench.Log(ex);
                    return null;
                }
            }

            public static CurveArray RequestMultipleCurveElementsSelection(UIDocument doc, string message)
            {
                try
                {
                    //CurveElement c = null;
                    //Curve cv = null;

                    Selection choices = doc.Selection;

                    choices.Elements.Clear();


                    //MessageBox.Show(message);
                    dynSettings.Bench.Log(message);

                    CurveArray ca = new CurveArray();
                    ISelectionFilter selFilter = new CurveSelectionFilter();
                    IList<Element> eList = doc.Selection.PickElementsByRectangle(//selFilter,
                        "Select multiple curves") as IList<Element>;


                    foreach (CurveElement c in eList)
                    {
                        if (c != null)
                        {
                            ca.Append(c.GeometryCurve as Curve);
                        }
                    }
                    return ca;
                }
                catch (Exception ex)
                {
                    dynSettings.Bench.Log(ex);
                    return null;
                }
            }

            public class CurveSelectionFilter : ISelectionFilter
            {
                public bool AllowElement(Element element)
                {
                    if (element.Category.Name == "Model Lines" || element.Category.Name == "Lines")
                    {
                        return true;
                    }
                    return false;
                }

                public bool AllowReference(Reference refer, XYZ point)
                {
                    return false;
                }
            }

            public static Face RequestFaceSelection(UIDocument doc, string message)
            {
                try
                {
                    Face f = null;

                    Selection choices = doc.Selection;

                    choices.Elements.Clear();

                    //MessageBox.Show(message);
                    dynSettings.Bench.Log(message);

                    //create some geometry options so that we computer references
                    Autodesk.Revit.DB.Options opts = new Options();
                    opts.ComputeReferences = true;
                    opts.DetailLevel = ViewDetailLevel.Medium;
                    opts.IncludeNonVisibleObjects = false;

                    Reference faceRef = doc.Selection.PickObject(ObjectType.Face);

                    if (faceRef != null)
                    {

                        GeometryObject geob = dynRevitSettings.Doc.Document.GetElement(faceRef).GetGeometryObjectFromReference(faceRef);

                        f = geob as Face;
                    }
                    return f;
                }
                catch (Exception ex)
                {
                    dynSettings.Bench.Log(ex);
                    return null;
                }


            }

            // MDJ TODO - this is really hacky. I want to just use the face but evaluating the ref fails later on in pointOnSurface, the ref just returns void, not sure why.
            public static Reference RequestFaceReferenceSelection(UIDocument doc, string message)
            {
                try
                {
                    Selection choices = doc.Selection;
                    choices.Elements.Clear();

                    //MessageBox.Show(message);
                    dynSettings.Bench.Log(message);

                    //create some geometry options so that we compute references
                    //Autodesk.Revit.DB.Options opts = new Options();
                    //opts.ComputeReferences = true;
                    //opts.DetailLevel = ViewDetailLevel.Medium;
                    //opts.IncludeNonVisibleObjects = false;

                    Reference faceRef = doc.Selection.PickObject(ObjectType.Face);

                    //if (faceRef != null)
                    //{
                    //    GeometryElement geom = dynRevitSettings.Doc.Document.GetElement(faceRef).get_Geometry(opts); 
                    //    dynRevitSettings.Doc.Document.GetElement(faceRef).GetGeometryObjectFromReference(faceRef);
                    //}
                    return faceRef;
                }
                catch (Exception ex)
                {
                    dynSettings.Bench.Log(ex);
                    return null;
                }
            }

            public static Form RequestFormSelection(UIDocument doc, string message)
            {
                try
                {
                    Form f = null;

                    Selection choices = doc.Selection;

                    choices.Elements.Clear();

                    //MessageBox.Show(message);
                    dynSettings.Bench.Log(message);

                    //create some geometry options so that we computer references
                    Autodesk.Revit.DB.Options opts = new Options();
                    opts.ComputeReferences = true;
                    opts.DetailLevel = ViewDetailLevel.Medium;
                    opts.IncludeNonVisibleObjects = false;

                    Reference formRef = doc.Selection.PickObject(ObjectType.Element);

                    if (formRef != null)
                    {
                        //the suggested new method didn't exist in API?
                        f = dynRevitSettings.Doc.Document.GetElement(formRef) as Form;
                    }
                    return f;
                }
                catch (Exception ex)
                {
                    dynSettings.Bench.Log(ex);
                    return null;
                }
            }

            public static FamilySymbol RequestFamilySymbolByInstanceSelection(UIDocument doc, string message, ref FamilyInstance fi)
            {
                try
                {
                    //FamilySymbol fs = null;

                    Selection choices = doc.Selection;

                    choices.Elements.Clear();

                    //MessageBox.Show(message);
                    dynSettings.Bench.Log(message);

                    Reference fsRef = doc.Selection.PickObject(ObjectType.Element);

                    if (fsRef != null)
                    {
                        fi = doc.Document.GetElement(fsRef) as FamilyInstance;

                        if (fi != null)
                        {
                            return fi.Symbol;
                        }
                        else return null;
                    }
                    else return null;
                }
                catch (Exception ex)
                {
                    dynSettings.Bench.Log(ex);
                    return null;
                }
            }

            public static FamilyInstance RequestFamilyInstanceSelection(UIDocument doc, string message)
            {
                try
                {
                    Selection choices = doc.Selection;

                    choices.Elements.Clear();

                    //MessageBox.Show(message);
                    dynSettings.Bench.Log(message);

                    Reference fsRef = doc.Selection.PickObject(ObjectType.Element);

                    if (fsRef != null)
                    {
                        return doc.Document.GetElement(fsRef.ElementId) as FamilyInstance;
                    }
                    else
                        return null;
                }
                catch (Exception ex)
                {
                    dynSettings.Bench.Log(ex);
                    return null;
                }
            }

            public static Element RequestLevelSelection(UIDocument doc, string message)
            {
                try
                {
                    Selection choices = doc.Selection;

                    choices.Elements.Clear();

                    //MessageBox.Show(message);
                    dynSettings.Bench.Log(message);

                    Reference fsRef = doc.Selection.PickObject(ObjectType.Element);

                    if (fsRef != null)
                    {
                        return doc.Document.GetElement(fsRef.ElementId) as Level;
                    }
                    else
                        return null;
                }
                catch (Exception ex)
                {
                    dynSettings.Bench.Log(ex);
                    return null;
                }
            }

            public static Element RequestAnalysisResultInstanceSelection(UIDocument doc, string message)
            {
                try
                {

                    View view = doc.ActiveView as View;

                    SpatialFieldManager sfm = SpatialFieldManager.GetSpatialFieldManager(view);
                    Element AnalysisResult;

                    if (sfm != null)
                    {
                        sfm.GetRegisteredResults();

                        Selection choices = doc.Selection;

                        choices.Elements.Clear();

                        //MessageBox.Show(message);
                        dynSettings.Bench.Log(message);

                        Reference fsRef = doc.Selection.PickObject(ObjectType.Element);

                        if (fsRef != null)
                        {
                            AnalysisResult = doc.Document.GetElement(fsRef.ElementId) as Element;

                            if (AnalysisResult != null)
                            {
                                return AnalysisResult;
                            }
                            else return null;
                        }
                        else return null;
                    }
                    else return null;
                }
                catch (Exception ex)
                {
                    dynSettings.Bench.Log(ex);
                    return null;
                }
            }
        }

        public static DynamoController_Revit Controller { get; internal set; }
    }
}
