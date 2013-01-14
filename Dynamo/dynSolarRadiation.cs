﻿//Copyright © Autodesk, Inc. 2012. All rights reserved.
//
//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at
//
//http://www.apache.org/licenses/LICENSE-2.0
//
//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Analysis;
using Autodesk.Revit.UI;
using Dynamo.Connectors;
using Dynamo.Utilities;
using Microsoft.FSharp.Collections;
using Expression = Dynamo.FScheme.Expression;
using System.Xml;
using System.Windows.Media;

namespace Dynamo.Elements
{
   [ElementName("Extract Solar Radiation Value")]
   [ElementCategory(BuiltinElementCategories.ANALYSIS)]
   [ElementDescription("Extracts and computes the average solar radiation value based on a CSV file.")]
   [RequiresTransaction(false)]
   public class dynComputeSolarRadiationValue : dynNode
   {
      public dynComputeSolarRadiationValue()
      {
         InPortData.Add(new PortData("raw", "The solar radiation data file", typeof(string)));
         OutPortData = new PortData("data", "The solar radiation computed data", typeof(double));

         base.RegisterInputsAndOutputs();
      }

      public override Expression Evaluate(FSharpList<Expression> args)
      {
         string data = ((Expression.String)args[0]).Item;

         var SumValue = 0.0;
         double doubleSRValue = 0;

         foreach (string line in data.Split(new char[] { '\r', '\n' }).Where(x => x.Length > 0))
         {
            string[] values = line.Split(',');

            //int i = 0;
            int intTest = 0;// used in TryParse below. returns 0 if not an int and >0 if an int.

            if (int.TryParse(values[0], out intTest)) // test the first value. if the first value is an int, then we know we are passed the header lines and into data
            {
               doubleSRValue = double.Parse(values[1]); // the 2nd value is the one we want
               SumValue += doubleSRValue; // compute the sum but adding current value with previous values
            }
         }

         return Expression.NewNumber(SumValue);
      }
   }

   [ElementName("Analysis Results by Selection")]
   [ElementCategory(BuiltinElementCategories.SELECTION)]
   [ElementDescription("Select an analysis result object from the document.")]
   [RequiresTransaction(true)]
   public class dynAnalysisResultsBySelection : dynNode
   {
      public dynAnalysisResultsBySelection()
      {
         OutPortData = new PortData("ar", "Analysis Results referenced by this operation.", typeof(Element));

         //add a button to the inputGrid on the dynElement
         Button analysisResultButt = new Button();
         this.inputGrid.Children.Add(analysisResultButt);
         analysisResultButt.Margin = new Thickness(0, 0, 0, 0);
         analysisResultButt.HorizontalAlignment = HorizontalAlignment.Center;
         analysisResultButt.VerticalAlignment = VerticalAlignment.Center;
         analysisResultButt.Click += new RoutedEventHandler(analysisResultButt_Click);
         analysisResultButt.Content = "Select AR";
         analysisResultButt.HorizontalAlignment = HorizontalAlignment.Stretch;
         analysisResultButt.VerticalAlignment = VerticalAlignment.Center;

         base.RegisterInputsAndOutputs();

      }

      public Element pickedAnalysisResult;

      public Element PickedAnalysisResult
      {
         get { return pickedAnalysisResult; }
         set
         {
            pickedAnalysisResult = value;
            NotifyPropertyChanged("PickedAnalysisResult");
            this.IsDirty = true;
         }
      }

      private ElementId analysisResultID;

      private ElementId AnalysisResultID
      {
         get { return analysisResultID; }
         set
         {
            analysisResultID = value;
            NotifyPropertyChanged("AnalysisResultID");
         }
      }
      void analysisResultButt_Click(object sender, RoutedEventArgs e)
      {
         PickedAnalysisResult =
            Dynamo.Utilities.SelectionHelper.RequestAnalysisResultInstanceSelection(
               this.UIDocument,
               "Select Analysis Result Object",
               dynElementSettings.SharedInstance
            );

         if (PickedAnalysisResult != null)
         {
            AnalysisResultID = PickedAnalysisResult.Id;
         }
      }


      public override Expression Evaluate(FSharpList<Expression> args)
      {
         if (PickedAnalysisResult != null)
         {
            if (PickedAnalysisResult.Id.IntegerValue == AnalysisResultID.IntegerValue) // sanity check
            {
               SpatialFieldManager dmu_sfm = dynElementSettings.SharedInstance.SpatialFieldManagerUpdated as SpatialFieldManager;

               if (pickedAnalysisResult.Id.IntegerValue == dmu_sfm.Id.IntegerValue)
               {
                  TaskDialog.Show("ah hah", "picked sfm equals saved one from dmu");
               }

               return Expression.NewContainer(this.PickedAnalysisResult);
            }
         }

         throw new Exception("No data selected!");
      }
   }

   [ElementName("SunPath Direction")]
   [ElementCategory(BuiltinElementCategories.ANALYSIS)]
   [ElementDescription("Returns the current Sun Path direction.")]
   [RequiresTransaction(false)]
   public class dynSunPathDirection : dynNode
   {
       System.Windows.Controls.TextBox tb;
       System.Windows.Controls.Button sunPathButt;
       Expression data = Expression.NewList(FSharpList<Expression>.Empty);


       public dynSunPathDirection()
       {

           OutPortData = new PortData("XYZ", "XYZ", typeof(XYZ));

           //add a button to the inputGrid on the dynElement
           sunPathButt = new System.Windows.Controls.Button();
           //this.inputGrid.Children.Add(sunPathButt);
           sunPathButt.Margin = new System.Windows.Thickness(0, 0, 0, 0);
           sunPathButt.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
           sunPathButt.VerticalAlignment = System.Windows.VerticalAlignment.Center;
           sunPathButt.Click += new System.Windows.RoutedEventHandler(registerButt_Click);
           sunPathButt.Content = "Use SunPath\nfrom Current View";
           sunPathButt.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
           sunPathButt.VerticalAlignment = System.Windows.VerticalAlignment.Center;

           tb = new System.Windows.Controls.TextBox();
           tb.Text = "No SunPath Registered";
           tb.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
           tb.VerticalAlignment = System.Windows.VerticalAlignment.Center;
           SolidColorBrush backgroundBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0, 0, 0, 0));
           tb.Background = backgroundBrush;
           tb.BorderThickness = new Thickness(0);
           tb.IsReadOnly = true;
           tb.IsReadOnlyCaretVisible = false;

           this.inputGrid.RowDefinitions.Add(new RowDefinition());
           this.inputGrid.RowDefinitions.Add(new RowDefinition());

           this.inputGrid.Children.Add(tb);
           this.inputGrid.Children.Add(sunPathButt);

           System.Windows.Controls.Grid.SetRow(sunPathButt, 0);
           System.Windows.Controls.Grid.SetRow(tb, 1);

           base.RegisterInputsAndOutputs();

           this.topControl.Height = 60;
           this.UpdateLayout();
       }


       /// <summary>
       /// Description of ShadowCalculatorUtils.
       /// NOTE: this is derived from Scott Connover's great class "Geometry API in Revit" from DevCamp 2012, source files accesed 6-8-12 from here 
       /// https://projectpoint.buzzsaw.com/_bz_rest/Web/Home/Index?folder=44#/_bz_rest/Web/Item/Items?folder=152&count=50&start=0&ownership=Homehttps://projectpoint.buzzsaw.com/_bz_rest/Web/Home/Index?folder=44#/_bz_rest/Web/Item/Items?folder=152&count=50&start=0&ownership=Home
       /// </summary>

       public static XYZ GetSunDirection(SunAndShadowSettings sunSettings)
       {
           //SunAndShadowSettings sunSettings = view.SunAndShadowSettings;

           XYZ initialDirection = XYZ.BasisY;

           //double altitude = sunSettings.Altitude;
           double altitude = sunSettings.GetFrameAltitude(sunSettings.ActiveFrame);
           Autodesk.Revit.DB.Transform altitudeRotation = Autodesk.Revit.DB.Transform.get_Rotation(XYZ.Zero, XYZ.BasisX, altitude);
           XYZ altitudeDirection = altitudeRotation.OfVector(initialDirection);

           //double azimuth = sunSettings.Azimuth;
           double azimuth = sunSettings.GetFrameAzimuth(sunSettings.ActiveFrame);
           double actualAzimuth = 2 * Math.PI - azimuth;
           Autodesk.Revit.DB.Transform azimuthRotation = Autodesk.Revit.DB.Transform.get_Rotation(XYZ.Zero, XYZ.BasisZ, actualAzimuth);
           XYZ sunDirection = azimuthRotation.OfVector(altitudeDirection);
           XYZ scaledSunVector = sunDirection.Multiply(100);

           return scaledSunVector;

       }

       public SunAndShadowSettings pickedSunAndShadowSettings;

       public SunAndShadowSettings PickedSunAndShadowSettings
       {
           get { return pickedSunAndShadowSettings; }
           set
           {
               pickedSunAndShadowSettings = value;
               NotifyPropertyChanged("PickedSunAndShadowSettings");
           }
       }

       private ElementId sunAndShadowSettingsID;

       private ElementId SunAndShadowSettingsID
       {
           get { return sunAndShadowSettingsID; }
           set
           {
               sunAndShadowSettingsID = value;
               NotifyPropertyChanged("SunAndShadowSettingsID");
           }
       }
       void registerButt_Click(object sender, System.Windows.RoutedEventArgs e)
       {
           //data = Expression.NewList(FSharpList<Expression>.Empty);

           View activeView = this.UIDocument.ActiveView;
           PickedSunAndShadowSettings = activeView.SunAndShadowSettings;


           if (PickedSunAndShadowSettings != null)
           {
               sunAndShadowSettingsID = activeView.SunAndShadowSettings.Id;
               this.RegisterEvalOnModified(sunAndShadowSettingsID); // register with the DMU, TODO - watch out for view changes, as sun is view specific
               XYZ sunVector = GetSunDirection(PickedSunAndShadowSettings);


               this.data = Expression.NewContainer(sunVector);

               this.tb.Text = PickedSunAndShadowSettings.Name;
           }
           else
           {
               //sunPathButt.Content = "Select Instance";
               this.tb.Text = "Nothing Selected";
           }
       }

       public override Expression Evaluate(FSharpList<Expression> args)
       {
           if (PickedSunAndShadowSettings.Id.IntegerValue == sunAndShadowSettingsID.IntegerValue) // sanity check
           {

               XYZ sunVector = GetSunDirection(PickedSunAndShadowSettings);
               this.data = Expression.NewContainer(sunVector);
               return data;
           }
           else
               throw new Exception("SANITY CHECK FAILED");
       }

       public override void SaveElement(XmlDocument xmlDoc, XmlElement dynEl)
       {
           //Debug.WriteLine(pd.Object.GetType().ToString());
           if (this.PickedSunAndShadowSettings != null)
           {
               XmlElement outEl = xmlDoc.CreateElement("instance");
               outEl.SetAttribute("id", this.PickedSunAndShadowSettings.Id.ToString());
               dynEl.AppendChild(outEl);
           }
       }

       public override void LoadElement(XmlNode elNode)
       {
           foreach (XmlNode subNode in elNode.ChildNodes)
           {
               if (subNode.Name.Equals("instance"))
               {
                   try
                   {
                       this.PickedSunAndShadowSettings = dynElementSettings.SharedInstance.Doc.Document.GetElement(
                          new ElementId(Convert.ToInt32(subNode.Attributes[0].Value))
                       ) as SunAndShadowSettings;
                       if (this.PickedSunAndShadowSettings != null)
                       {
                           sunAndShadowSettingsID = PickedSunAndShadowSettings.Id;
                           this.tb.Text = this.PickedSunAndShadowSettings.Name;
                           this.sunPathButt.Content = "Use SunPath from Current View";
                       }
                   }
                   catch { }
               }
           }
       }

   }
}
