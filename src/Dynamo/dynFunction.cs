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
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;
using Dynamo.Connectors;
using Dynamo.FSchemeInterop.Node;
using Dynamo.Utilities;
using Microsoft.FSharp.Collections;
using Expression = Dynamo.FScheme.Expression;
using Value = Dynamo.FScheme.Value;
using Dynamo.FSchemeInterop;
using System.Windows.Media.Effects;

namespace Dynamo.Nodes
{
    [IsInteractive(false)]
    public class dynFunction : dynBuiltinFunction
    {
        public dynFunction(IEnumerable<string> inputs, IEnumerable<string> outputs, string symbol)
            : base(symbol)
        {
            //Set inputs and output
            SetInputs(inputs);
            foreach (var output in outputs)
                OutPortData.Add(new PortData(output, "function output", typeof(object)));

            //Set the nickname
            NodeUI.NickName = symbol;

            //Add a drop-shadow.
            ((DropShadowEffect)NodeUI.elementRectangle.Effect).Opacity = 1;

            //Setup double-click behavior
            NodeUI.MouseDoubleClick += delegate
            {
                Controller.DisplayFunction(symbol);
            };

            NodeUI.RegisterAllPorts();
        }

        public dynFunction()
            : base(null)
        {
            //Setup double-click behavior
            NodeUI.MouseDoubleClick += delegate
            {
                Controller.DisplayFunction(Symbol);
            };

            //Add a drop-shadow
            ((DropShadowEffect)NodeUI.elementRectangle.Effect).Opacity = 1;
        }

        public override bool RequiresRecalc
        {
            get
            {
                //Do we already know we're dirty?
                bool baseDirty = base.RequiresRecalc;
                if (baseDirty)
                    return true;

                //Initialize recursive function detection construct.
                bool start = _startTag;
                _startTag = true;

                //If we've already been here, then we're not dirty.
                if (_taggedSymbols.Contains(Symbol))
                    return false;
                //Remember we've been here.
                _taggedSymbols.Add(Symbol);

                if (!Controller.FunctionDict.ContainsKey(Symbol))
                {
                    Bench.Log("WARNING -- No implementation found for node: " + Symbol);
                    NodeUI.Error("Could not find .dyf definition file for this node.");

                    if (!start)
                    {
                        _startTag = false;
                        _taggedSymbols.Clear();
                    }

                    return false;
                }

                //TODO: bugged? 
                //Solution: pass func workspace to dynFunction, hook the Modified event, set IsDirty to true when modified.
                var ws = Controller.FunctionDict[Symbol]; //TODO: Refactor
                bool dirtyInternals = ws.Nodes.Any(e => e.RequiresRecalc);

                //If we started the traversal here, clean up.
                if (!start)
                {
                    _startTag = false;
                    _taggedSymbols.Clear();
                }

                return dirtyInternals;
            }
            set
            {
                //Set the base value.
                base.RequiresRecalc = value;
                //If we're clean, then notify all internals.
                if (!value)
                {
                    //Recursion detection start.
                    bool start = _startTag;
                    _startTag = true;

                    //If we've been here, then we're done.
                    if (_taggedSymbols.Contains(Symbol))
                        return;
                    //Remember
                    _taggedSymbols.Add(Symbol);

                    if (!Controller.FunctionDict.ContainsKey(Symbol))
                    {
                        Bench.Log("WARNING -- No implementation found for node: " + Symbol);
                        NodeUI.Error("Could not find .dyf definition file for this node.");

                        if (!start)
                        {
                            _startTag = false;
                            _taggedSymbols.Clear();
                        }

                        return;
                    }

                    //Notifiy all internals that we're clean.
                    var ws = Controller.FunctionDict[Symbol]; //TODO: Refactor
                    foreach (var e in ws.Nodes)
                        e.RequiresRecalc = false;

                    //If we started traversal here, cleanup.
                    if (!start)
                    {
                        _startTag = false;
                        _taggedSymbols.Clear();
                    }
                }
            }
        }

        /// <summary>
        /// Sets the inputs of this function.
        /// </summary>
        /// <param name="inputs"></param>
        public void SetInputs(IEnumerable<string> inputs)
        {
            int i = 0;
            foreach (string input in inputs)
            {
                if (InPortData.Count > i)
                {
                    InPortData[i].NickName = input;
                }
                else
                {
                    InPortData.Add(new PortData(input, "Input #" + (i + 1), typeof(object)));
                }

                i++;
            }

            if (i < InPortData.Count)
            {
                for (var k = i; k < InPortData.Count; k++)
                    NodeUI.InPorts[k].KillAllConnectors();

                InPortData.RemoveRange(i, InPortData.Count - i);
            }
        }

        public void SetOutputs(IEnumerable<string> outputs)
        {
            int i = 0;
            foreach (string output in outputs)
            {
                if (OutPortData.Count > i)
                {
                    OutPortData[i].NickName = output;
                }
                else
                {
                    OutPortData.Add(new PortData(output, "Output #" + (i + 1), typeof(object)));
                }

                i++;
            }

            if (i < OutPortData.Count)
            {
                for (var k = i; k < OutPortData.Count; k++)
                    NodeUI.OutPorts[k].KillAllConnectors();

                OutPortData.RemoveRange(i, OutPortData.Count - i);
            }
        }

        public override void SaveElement(XmlDocument xmlDoc, XmlElement dynEl)
        {
            //Debug.WriteLine(pd.Object.GetType().ToString());
            XmlElement outEl = xmlDoc.CreateElement("Symbol");
            outEl.SetAttribute("value", Symbol);
            dynEl.AppendChild(outEl);

            outEl = xmlDoc.CreateElement("Inputs");
            foreach (var input in InPortData.Select(x => x.NickName))
            {
                var inputEl = xmlDoc.CreateElement("Input");
                inputEl.SetAttribute("value", input);
                outEl.AppendChild(inputEl);
            }
            dynEl.AppendChild(outEl);

            outEl = xmlDoc.CreateElement("Outputs");
            foreach (var output in OutPortData.Select(x => x.NickName))
            {
                var outputEl = xmlDoc.CreateElement("Output");
                outputEl.SetAttribute("value", output);
                outEl.AppendChild(outputEl);
            }
            dynEl.AppendChild(outEl);
        }

        public override void LoadElement(XmlNode elNode)
        {
            foreach (XmlNode subNode in elNode.ChildNodes)
            {
                if (subNode.Name.Equals("Symbol"))
                {
                    Symbol = subNode.Attributes[0].Value;
                }
                else if (subNode.Name.Equals("Outputs"))
                {
                    int i = 0;
                    foreach (XmlNode outputNode in subNode.ChildNodes)
                    {
                        var data = new PortData(outputNode.Attributes[0].Value, "Output #" + (i + 1), typeof(object));

                        if (OutPortData.Count > i)
                        {
                            OutPortData[i] = data;
                        }
                        else
                        {
                            OutPortData.Add(data);
                        }

                        i++;
                    }
                }
                else if (subNode.Name.Equals("Inputs"))
                {
                    int i = 0;
                    foreach (XmlNode inputNode in subNode.ChildNodes)
                    {
                        var data = new PortData(inputNode.Attributes[0].Value, "Input #" + (i + 1), typeof(object));

                        if (InPortData.Count > i)
                        {
                            InPortData[i] = data;
                        }
                        else
                        {
                            InPortData.Add(data);
                        }

                        i++;
                    }
                }
                #region Legacy output support
                else if (subNode.Name.Equals("Output"))
                {
                    var data = new PortData(subNode.Attributes[0].Value, "function output", typeof(object));

                    if (OutPortData.Any())
                        OutPortData[0] = data;
                    else
                        OutPortData.Add(data);
                }
                #endregion
            }

            NodeUI.RegisterAllPorts();
        }

        public override Value Evaluate(FSharpList<Value> args)
        {
            var procedure = Controller.FSchemeEnvironment.LookupSymbol(Symbol);
            if (procedure.IsFunction)
            {
                return (procedure as Value.Function).Item.Invoke(args);
            }
            else
                return base.Evaluate(args);
        }

        public override void Destroy()
        {
            bool start = _startTag;
            _startTag = true;

            if (_taggedSymbols.Contains(Symbol))
                return;
            _taggedSymbols.Add(Symbol);

            if (!Controller.FunctionDict.ContainsKey(Symbol))
            {
                Bench.Log("WARNING -- No implementation found for node: " + Symbol);
                NodeUI.Error("Could not find .dyf definition file for this node.");

                if (!start)
                {
                    _startTag = false;
                    _taggedSymbols.Clear();
                }

                return;
            }

            var ws = Controller.FunctionDict[Symbol]; //TODO: Refactor
            foreach (var el in ws.Nodes)
                el.Destroy();

            if (!start)
            {
                _startTag = false;
                _taggedSymbols.Clear();
            }
        }
    }

    [NodeName("Output")]
    [NodeCategory(BuiltinNodeCategories.PRIMITIVES)]
    [NodeDescription("A function output")]
    [IsInteractive(false)]
    public class dynOutput : dynNode
    {
        TextBox tb;

        public dynOutput()
        {
            //add a text box to the input grid of the control
            tb = new TextBox();
            tb.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            tb.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            NodeUI.inputGrid.Children.Add(tb);
            System.Windows.Controls.Grid.SetColumn(tb, 0);
            System.Windows.Controls.Grid.SetRow(tb, 0);
            tb.Text = "";
            //tb.KeyDown += new System.Windows.Input.KeyEventHandler(tb_KeyDown);
            //tb.LostFocus += new System.Windows.RoutedEventHandler(tb_LostFocus);

            //turn off the border
            SolidColorBrush backgroundBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0, 0, 0, 0));
            tb.Background = backgroundBrush;
            tb.BorderThickness = new Thickness(0);

            InPortData.Add(new PortData("", "", typeof(object)));

            NodeUI.RegisterAllPorts();
        }

        public override bool RequiresRecalc
        {
            get
            {
                return false;
            }
            set { }
        }

        public string Symbol
        {
            get { return tb.Text; }
            set { tb.Text = value; }
        }

        public override void SaveElement(XmlDocument xmlDoc, XmlElement dynEl)
        {
            //Debug.WriteLine(pd.Object.GetType().ToString());
            XmlElement outEl = xmlDoc.CreateElement("Symbol");
            outEl.SetAttribute("value", Symbol);
            dynEl.AppendChild(outEl);
        }

        public override void LoadElement(XmlNode elNode)
        {
            foreach (XmlNode subNode in elNode.ChildNodes)
            {
                if (subNode.Name == "Symbol")
                {
                    Symbol = subNode.Attributes[0].Value;
                }
            }
        }
    }

    [NodeName("Variable")]
    [NodeCategory(BuiltinNodeCategories.PRIMITIVES)]
    [NodeDescription("A function variable")]
    [IsInteractive(false)]
    public class dynSymbol : dynNode
    {
        TextBox tb;

        public dynSymbol()
        {
            //add a text box to the input grid of the control
            tb = new TextBox();
            tb.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            tb.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            NodeUI.inputGrid.Children.Add(tb);
            System.Windows.Controls.Grid.SetColumn(tb, 0);
            System.Windows.Controls.Grid.SetRow(tb, 0);
            tb.Text = "";
            //tb.KeyDown += new System.Windows.Input.KeyEventHandler(tb_KeyDown);
            //tb.LostFocus += new System.Windows.RoutedEventHandler(tb_LostFocus);

            //turn off the border
            SolidColorBrush backgroundBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0, 0, 0, 0));
            tb.Background = backgroundBrush;
            tb.BorderThickness = new Thickness(0);

            OutPortData.Add(new PortData("", "Symbol", typeof(object)));

            NodeUI.RegisterAllPorts();
        }

        public override bool RequiresRecalc
        {
            get
            {
                return false;
            }
            set { }
        }

        public string Symbol
        {
            get { return tb.Text; }
            set { tb.Text = value; }
        }

        protected internal override INode Build(Dictionary<dynNode, Dictionary<int, INode>> preBuilt, int outPort)
        {
            Dictionary<int, INode> result;
            if (!preBuilt.TryGetValue(this, out result))
            {
                result = new Dictionary<int, INode>();
                result[outPort] = new SymbolNode(NodeUI.GUID.ToString());
                preBuilt[this] = result;
            }
            return result[outPort];
        }

        public override void SaveElement(XmlDocument xmlDoc, XmlElement dynEl)
        {
            //Debug.WriteLine(pd.Object.GetType().ToString());
            XmlElement outEl = xmlDoc.CreateElement("Symbol");
            outEl.SetAttribute("value", Symbol);
            dynEl.AppendChild(outEl);
        }

        public override void LoadElement(XmlNode elNode)
        {
            foreach (XmlNode subNode in elNode.ChildNodes)
            {
                if (subNode.Name == "Symbol")
                {
                    Symbol = subNode.Attributes[0].Value;
                }
            }
        }
    }

    #region Disabled Anonymous Function Node
    //[RequiresTransaction(false)]
    //[IsInteractive(false)]
    //public class dynAnonFunction : dynElement
    //{
    //   private INode entryPoint;

    //   public dynAnonFunction(IEnumerable<string> inputs, string output, INode entryPoint)
    //   {
    //      int i = 1;
    //      foreach (string input in inputs)
    //      {
    //         InPortData.Add(new PortData(null, input, "Input #" + i++, typeof(object)));
    //      }

    //      OutPortData = new PortData(null, output, "function output", typeof(object));

    //      entryPoint = entryPoint;

    //      NodeUI.RegisterInputsAndOutput();
    //   }

    //   protected internal override ProcedureCallNode Compile(IEnumerable<string> portNames)
    //   {
    //      return new AnonymousFunctionNode(portNames, entryPoint);
    //   }
    //}
    #endregion
}
