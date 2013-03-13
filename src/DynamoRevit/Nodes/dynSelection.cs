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
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Linq;
using System.Text;
using System.Xml;
using Autodesk.Revit.DB;
using Dynamo.Utilities;
using Dynamo.Connectors;

using Dynamo.Revit.SyncedNodeExtensions; //Gives the RegisterEval... methods

using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Value = Dynamo.FScheme.Value;
using Dynamo.FSchemeInterop;

namespace Dynamo.Nodes
{
    [IsInteractive(true)]
    public abstract class dynElementSelection: dynNodeWithOneOutput
    {
        TextBox tb;
        System.Windows.Controls.Button selectButton;

        protected dynElementSelection(PortData outPortData)
        {
            OutPortData.Add(outPortData);

            //add a button to the inputGrid on the dynElement
            selectButton = new System.Windows.Controls.Button();
            selectButton.Margin = new System.Windows.Thickness(0, 0, 0, 0);
            selectButton.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            selectButton.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            selectButton.Click += new System.Windows.RoutedEventHandler(selectButton_Click);
            selectButton.Content = "Select Instance";
            selectButton.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            selectButton.VerticalAlignment = System.Windows.VerticalAlignment.Center;

            tb = new TextBox();
            tb.Text = "Nothing Selected";
            tb.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            tb.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            SolidColorBrush backgroundBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0, 0, 0, 0));
            tb.Background = backgroundBrush;
            tb.BorderThickness = new Thickness(0);
            tb.IsReadOnly = true;
            tb.IsReadOnlyCaretVisible = false;

            NodeUI.SetRowAmount(2);

            NodeUI.inputGrid.Children.Add(tb);
            NodeUI.inputGrid.Children.Add(selectButton);

            System.Windows.Controls.Grid.SetRow(selectButton, 0);
            System.Windows.Controls.Grid.SetRow(tb, 1);

            NodeUI.RegisterAllPorts();

            NodeUI.topControl.Height = 60;
            NodeUI.topControl.Width = 200;
            NodeUI.UpdateLayout();
        }

        private void selectButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            this.selectButton.IsEnabled = false;
            IdlePromise.ExecuteOnIdle(new Action(
                delegate
                {
                    this.OnSelectClick();
                    this.selectButton.IsEnabled = true;
                }
            ));
        }

        /// <summary>
        /// Callback for when the "Select" button has been clicked.
        /// </summary>
        /// 
        /// 
        protected abstract void OnSelectClick();

        private Element selected;
        /// <summary>
        /// The Element which is selected. Setting this property will automatically register the Element
        /// for proper updating, and will update this node's IsDirty value.
        /// </summary>
        public virtual Element SelectedElement
        {
            get { return selected; }
            set
            {
                var dirty = false;
                if (this.selected != null)
                {
                    if (value != null && value.Id.Equals(this.selected.Id))
                        return;

                    dirty = true;
                    this.UnregisterEvalOnModified(this.selected.Id);
                }
                else
                {
                    dirty = value != null;
                }

                this.selected = value;
                if (value != null)
                {
                    this.RegisterEvalOnModified(
                       value.Id,
                       delAction: delegate { this.selected = null; this.SelectedElement = null; }
                    );

                    this.tb.Text = this.SelectionText;
                    this.selectButton.Content = "Change";
                }
                else
                {
                    this.tb.Text = "Nothing Selected.";
                    this.selectButton.Content = "Select";
                }

                if (dirty)
                    this.RequiresRecalc = true;
            }
        }

        /// <summary>
        /// Determines what the text should read on the node when the selection has been changed.
        /// Is ignored in the case where nothing is selected.
        /// </summary>
        protected abstract string SelectionText { get; }

        public override Value Evaluate(FSharpList<Value> args)
        {
            if (this.SelectedElement == null)
                throw new Exception("Nothing selected.");

            return Value.NewContainer(this.SelectedElement);
        }

        public override void SaveElement(XmlDocument xmlDoc, XmlElement dynEl)
        {
            //Debug.WriteLine(pd.Object.GetType().ToString());
            if (this.SelectedElement != null)
            {
                XmlElement outEl = xmlDoc.CreateElement("instance");
                outEl.SetAttribute("id", this.SelectedElement.Id.ToString());
                dynEl.AppendChild(outEl);
            }
        }

        public override void LoadElement(XmlNode elNode)
        {
            foreach (XmlNode subNode in elNode.ChildNodes)
            {
                if (subNode.Name.Equals("instance"))
                {
                    Element saved = null;
                    var id = new ElementId(Convert.ToInt32(subNode.Attributes[0].Value));
                    try
                    {
                        saved = dynRevitSettings.Doc.Document.GetElement(id) as FamilyInstance;
                    }
                    catch
                    {
                        this.Bench.Log("Unable to find element with ID: " + id.IntegerValue);
                    }
                    this.SelectedElement = saved;
                }
            }
        }
    }

    [IsInteractive(true)]
    public abstract class dynMultipleElementSelection: dynNodeWithOneOutput
    {
        TextBox tb;
        System.Windows.Controls.Button selectButton;

        protected dynMultipleElementSelection(PortData outData)
        {
            OutPortData.Add(outData);

            //add a button to the inputGrid on the dynElement
            selectButton = new System.Windows.Controls.Button();
            selectButton.Margin = new System.Windows.Thickness(0, 0, 0, 0);
            selectButton.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            selectButton.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            selectButton.Click += new System.Windows.RoutedEventHandler(selectButton_Click);
            selectButton.Content = "Select Instances";
            selectButton.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            selectButton.VerticalAlignment = System.Windows.VerticalAlignment.Center;

            tb = new TextBox();
            tb.Text = "Nothing Selected";
            tb.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            tb.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            SolidColorBrush backgroundBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0, 0, 0, 0));
            tb.Background = backgroundBrush;
            tb.BorderThickness = new Thickness(0);
            tb.IsReadOnly = true;
            tb.IsReadOnlyCaretVisible = false;

            NodeUI.SetRowAmount(2);

            NodeUI.inputGrid.Children.Add(tb);
            NodeUI.inputGrid.Children.Add(selectButton);

            System.Windows.Controls.Grid.SetRow(selectButton, 0);
            System.Windows.Controls.Grid.SetRow(tb, 1);

            NodeUI.RegisterAllPorts();

            NodeUI.topControl.Height = 60;
            NodeUI.UpdateLayout();
        }

        private void selectButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            this.selectButton.IsEnabled = false;
            IdlePromise.ExecuteOnIdle(new Action(
                delegate
                {
                    this.OnSelectClick();
                    this.selectButton.IsEnabled = true;
                }
            ));
        }

        /// <summary>
        /// Callback for when the "Select" button has been clicked.
        /// </summary>
        /// 
        /// 
        protected abstract void OnSelectClick();

        private IList<Element> selected;
        /// <summary>
        /// The Element which is selected. Setting this property will automatically register the Element
        /// for proper updating, and will update this node's IsDirty value.
        /// </summary>
        public virtual IList<Element> SelectedElements
        {
            get { return selected; }
            set
            {
                var dirty = false;
                if (this.selected != null)
                {
                    foreach (Element selectedElement in this.selected)
                    {
                        foreach (Element previousElement in value)
                        {

                            if (previousElement != null && previousElement.Id.Equals(selectedElement.Id))
                                return;

                            dirty = true;
                            this.UnregisterEvalOnModified(selectedElement.Id);
                        }
                    }
                }
                else
                {
                    dirty = value != null;
                }

                this.selected = value;
                if (value != null)
                {
                    foreach (Element previousElement in value)
                    {
                        this.RegisterEvalOnModified(
                           previousElement.Id,
                           delAction: delegate { this.selected = null; this.SelectedElements = null; }
                        );
                    }

                    this.tb.Text = this.SelectionText;
                    this.selectButton.Content = "Change";
                }
                else
                {
                    this.tb.Text = "Nothing Selected.";
                    this.selectButton.Content = "Select";
                }

                if (dirty)
                    this.RequiresRecalc = true;
            }
        }

        /// <summary>
        /// Determines what the text should read on the node when the selection has been changed.
        /// Is ignored in the case where nothing is selected.
        /// </summary>
        protected abstract string SelectionText { get; }

        public override Value Evaluate(FSharpList<Value> args)
        {
            if (this.SelectedElements == null)
                throw new Exception("Nothing selected.");

            return Value.NewContainer(this.SelectedElements);
        }

        public override void SaveElement(XmlDocument xmlDoc, XmlElement dynEl)
        {
            //Debug.WriteLine(pd.Object.GetType().ToString());
            if (this.SelectedElements != null)
            {
                foreach (Element selectedElement in this.SelectedElements)
                {
                    XmlElement outEl = xmlDoc.CreateElement("instance");
                    outEl.SetAttribute("id", selectedElement.Id.ToString());
                    dynEl.AppendChild(outEl);
                }
            }
        }

        public override void LoadElement(XmlNode elNode)
        {
            foreach (XmlNode subNode in elNode.ChildNodes)
            {
                if (subNode.Name.Equals("instance"))
                {
                    Element saved = null;
                    var id = new ElementId(Convert.ToInt32(subNode.Attributes[0].Value));
                    try
                    {
                        saved = dynRevitSettings.Doc.Document.GetElement(id) as FamilyInstance;
                    }
                    catch
                    {
                        this.Bench.Log("Unable to find element with ID: " + id.IntegerValue);
                    }
                    this.SelectedElements.Add(saved);
                }
            }
        }
    }

    [NodeName("Family Instance by Selection")]
    [NodeCategory(BuiltinNodeCategories.SELECTION)]
    [NodeDescription("Select a family instance from the document.")]
    public class dynFamilyInstanceCreatorSelection : dynElementSelection
    {
        public dynFamilyInstanceCreatorSelection()
            : base(new PortData("fi", "Family instances created by this operation.", typeof(FamilyInstance)))
        { }

        protected override void OnSelectClick()
        {
            this.SelectedElement = dynRevitSettings.SelectionHelper.RequestFamilyInstanceSelection(
               dynRevitSettings.Doc, "Select Massing Family Instance"
            );
        }

        protected override string SelectionText
        {
            get { return this.SelectedElement.Name; }
        }
    }

    [NodeName("Divided Surface by Selection")]
    [NodeCategory(BuiltinNodeCategories.SELECTION)]
    [NodeDescription("Select a divided surface from the document.")]
    public class dynDividedSurfaceBySelection : dynElementSelection
    {
        Value data;

        public dynDividedSurfaceBySelection()
            : base(new PortData("srf", "The divided surface family instance(s)", typeof(object)))
        { }

        public override Value Evaluate(FSharpList<Value> args)
        {
            var result = new List<List<FamilyInstance>>();

            //"Get an interface to the divided surfaces on this element."
            //TODO: do we want to select a face instead and try to get
            //the divided surface that way?
            DividedSurfaceData dsd = this.SelectedElement.GetDividedSurfaceData();

            if (dsd != null)
            {
                foreach (Reference r in dsd.GetReferencesWithDividedSurfaces())
                {
                    DividedSurface ds = dsd.GetDividedSurfaceForReference(r);

                    GridNode gn = new GridNode();

                    int u = 0;
                    while (u < ds.NumberOfUGridlines)
                    {

                        var lst = new List<FamilyInstance>();

                        gn.UIndex = u;

                        int v = 0;
                        while (v < ds.NumberOfVGridlines)
                        {
                            gn.VIndex = v;

                            //"Reports whether a grid node is a "seed node," a node that is associated with one or more tiles."
                            if (ds.IsSeedNode(gn))
                            {
                                FamilyInstance fi
                                  = ds.GetTileFamilyInstance(gn, 0);

                                //put the family instance into the tree
                                lst.Add(fi);
                            }
                            v = v + 1;
                        }

                        //don't add list if it's empty
                        if(lst.Count() > 0)
                            result.Add(lst);

                        u = u + 1;
                    }
                }

                this.data = Value.NewList(
                   Utils.SequenceToFSharpList(
                      result.Select(
                         row => Value.NewList(
                            Utils.SequenceToFSharpList(
                               row.Select(Value.NewContainer)
                            )
                         )
                      )
                   )
                );
            }

            return data;
        }

        protected override string SelectionText
        {
            get { return "Element ID: " + this.SelectedElement.Id; }
        }

        /*
        public override Element SelectedElement
        {
            get
            {
                return base.SelectedElement;
            }
            set
            {
                base.SelectedElement = value;

                var result = new List<List<FamilyInstance>>();

                DividedSurfaceData dsd = this.SelectedElement.GetDividedSurfaceData();

                if (dsd != null)
                {
                    foreach (Reference r in dsd.GetReferencesWithDividedSurfaces())
                    {
                        DividedSurface ds = dsd.GetDividedSurfaceForReference(r);

                        GridNode gn = new GridNode();

                        int u = 0;
                        while (u < ds.NumberOfUGridlines)
                        {

                            var lst = new List<FamilyInstance>();

                            gn.UIndex = u;

                            int v = 0;
                            while (v < ds.NumberOfVGridlines)
                            {
                                gn.VIndex = v;

                                if (ds.IsSeedNode(gn))
                                {
                                    FamilyInstance fi
                                      = ds.GetTileFamilyInstance(gn, 0);

                                    //put the family instance into the tree
                                    lst.Add(fi);
                                }
                                v = v + 1;
                            }

                            result.Add(lst);

                            u = u + 1;
                        }
                    }

                    this.data = Value.NewList(
                       Utils.SequenceToFSharpList(
                          result.Select(
                             row => Value.NewList(
                                Utils.SequenceToFSharpList(
                                   row.Select(Value.NewContainer)
                                )
                             )
                          )
                       )
                    );
                }
            }
        }
        */

        protected override void OnSelectClick()
        {
            this.SelectedElement = null;
            this.SelectedElement = dynRevitSettings.SelectionHelper.RequestFormSelection(
               dynRevitSettings.Doc, "Select a form element."
            );
        }
    }

    [NodeName("Face by Selection")]
    [NodeCategory(BuiltinNodeCategories.SELECTION)]
    [NodeDescription("Select a face from the document.")]
    public class dynFormElementBySelection : dynElementSelection
    {
        Reference f;

        public dynFormElementBySelection()
            : base(new PortData("face", "The face", typeof(object)))
        { }

        protected override void OnSelectClick()
        {
            var doc = dynRevitSettings.Doc;

            f = dynRevitSettings.SelectionHelper.RequestFaceReferenceSelection(
               doc, "Select a face."
            );
            this.SelectedElement = doc.Document.GetElement(f);
        }

        public override Value Evaluate(FSharpList<Value> args)
        {
            return Value.NewContainer(f);
        }

        protected override string SelectionText
        {
            get { return "Face ID: " + this.SelectedElement.Id; }
        }
    }

    [NodeName("Curve by Selection")]
    [NodeCategory(BuiltinNodeCategories.SELECTION)]
    [NodeDescription("Select a curve from the document.")] //or set of curves in the future
    public class dynCurvesBySelection : dynElementSelection
    {
        public dynCurvesBySelection()
            : base(new PortData("curve", "The curve", typeof(CurveElement)))
        { }

        protected override void OnSelectClick()
        {
            this.SelectedElement = dynRevitSettings.SelectionHelper.RequestCurveElementSelection(
               dynRevitSettings.Doc, "Select a curve."
            );

        }

        protected override string SelectionText
        {
            get { return "Curve ID: " + this.SelectedElement.Id; }
        }
    }

    [NodeName("Curves by Selection")]
    [NodeCategory(BuiltinNodeCategories.SELECTION)]
    [NodeDescription("Select a set of curves from the document.")]
    public class dynMultipleCurvesBySelection : dynMultipleElementSelection
    {
        CurveArray curves;

        public dynMultipleCurvesBySelection()
            : base(new PortData("curves", "The curves", typeof(CurveElement)))
        { }

        protected override void OnSelectClick()
        {
            curves = dynRevitSettings.SelectionHelper.RequestMultipleCurveElementsSelection(
               dynRevitSettings.Doc, "Select a set of curves."
            );
            this.SelectedElements.Clear();
            try
            {
                foreach (Element e in curves)
                {
                    this.SelectedElements.Add(e);
                }
            }
            catch (Exception)
            {
            }
        }

        string formatSelectionText(IList<Element> elements)
        {
            string selectionText = "";
            foreach (Element e in elements)
            {
                selectionText = selectionText + " " + e.Id.ToString();
            }
            return selectionText;
        }

        protected override string SelectionText
        {
            get
            {
                return "Curve IDs:" + formatSelectionText(this.SelectedElements);
            }
        }
    }

    [NodeName("Point by Selection")]
    [NodeCategory(BuiltinNodeCategories.SELECTION)]
    [NodeDescription("Select a reference point from the document.")]
    public class dynPointBySelection : dynElementSelection
    {
        public dynPointBySelection() :
            base(new PortData("pt", "The point", typeof(ReferencePoint)))
        { }

        protected override void OnSelectClick()
        {
            this.SelectedElement = dynRevitSettings.SelectionHelper.RequestReferencePointSelection(
               dynRevitSettings.Doc, "Select a reference point."
            );
        }

        protected override string SelectionText
        {
            get { return this.SelectedElement.Name + " (" + this.SelectedElement.Id + ")"; }
        }
    }

    [NodeName("Level by Selection")]
    [NodeCategory(BuiltinNodeCategories.SELECTION)]
    [NodeDescription("Select a level from the document.")]
    public class dynLevelBySelection : dynElementSelection
    {
        public dynLevelBySelection() :
            base(new PortData("lvl", "The selected level", typeof(Level)))
        { }

        protected override void OnSelectClick()
        {
            this.SelectedElement = dynRevitSettings.SelectionHelper.RequestLevelSelection(
               dynRevitSettings.Doc, "Select a level."
            );
        }

        protected override string SelectionText
        {
            get { return this.SelectedElement.Name + " (" + this.SelectedElement.Id + ")"; }
        }
    }
}


