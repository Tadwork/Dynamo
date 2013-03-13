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
using System.Text;

using System.Windows.Controls;

using Dynamo;
using Dynamo.Nodes;
using Dynamo.Connectors;
using Dynamo.FSchemeInterop;
using Dynamo.Utilities;
using Value = Dynamo.FScheme.Value;

using Microsoft.FSharp.Collections;

using IronPython;
using IronPython.Hosting;
using IronPython.Runtime;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using System.Windows;
using System.Xml;
using Microsoft.FSharp.Core;

namespace Dynamo.Nodes.PythonNode
{
    internal static class Converters
    {
        internal static Value convertPyFunction(Func<IList<dynamic>, dynamic> pyf)
        {
            return Value.NewFunction(
                FSharpFunc<FSharpList<Value>, Value>.FromConverter(
                    args =>
                        convertToValue(
                            pyf(args.Select(ex => convertFromValue(ex)).ToList()))));
        }

        internal static Value convertToValue(dynamic data)
        {
            if (data is Value)
                return data;
            else if (data is string)
                return Value.NewString(data);
            else if (data is double)
                return Value.NewNumber(data);
            else if (data is IEnumerable<dynamic>)
            {
                FSharpList<Value> result = FSharpList<Value>.Empty;

                data.reverse();

                foreach (var x in data)
                {
                    result = FSharpList<Value>.Cons(convertToValue(x), result);
                }

                return Value.NewList(result);
            }
            //else if (data is PythonFunction)
            //{
            //   return FuncContainer.MakeFunction(
            //      new FScheme.ExternFunc(
            //         args =>
            //            convertToValue(
            //               data(args.Select(ex => convertFromValue(ex)))
            //            )
            //      )
            //   );
            //}
            //else if (data is Func<dynamic, dynamic>)
            //{
            //   return Value.NewCurrent(FuncContainer.MakeContinuation(
            //      new Continuation(
            //         exp =>
            //            convertToValue(
            //               data(convertFromValue(exp))
            //            )
            //      )
            //   ));
            //}
            else
                return Value.NewContainer(data);
        }

        internal static dynamic convertFromValue(Value exp)
        {
            if (exp.IsList)
                return ((Value.List)exp).Item.Select(x => convertFromValue(x)).ToList();
            else if (exp.IsNumber)
                return ((Value.Number)exp).Item;
            else if (exp.IsString)
                return ((Value.String)exp).Item;
            else if (exp.IsContainer)
                return ((Value.Container)exp).Item;
            //else if (exp.IsFunction)
            //{
            //   return new Func<IList<dynamic>, dynamic>(
            //      args =>
            //         ((Value.Function)exp).Item
            //            .Invoke(ExecutionEnvironment.IDENT)
            //            .Invoke(Utils.convertSequence(args.Select(
            //               x => (Value)Converters.convertToValue(x)
            //            )))
            //   );
            //}
            //else if (exp.IsSpecial)
            //{
            //   return new Func<IList<dynamic>, dynamic>(
            //      args =>
            //         ((Value.Special)exp).Item
            //            .Invoke(ExecutionEnvironment.IDENT)
            //            .Invoke(
            //}
            //else if (exp.IsCurrent)
            //{
            //   return new Func<dynamic, dynamic>(
            //      ex => 
            //         Converters.convertFromValue(
            //            ((Value.Current)exp).Item.Invoke(Converters.convertToValue(ex))
            //         )
            //   );
            //}
            else
                throw new Exception("Not allowed to pass Functions into a Python Script.");
        }
    }

    internal class DynPythonEngine
    {
        private ScriptEngine engine;
        private ScriptSource source;

        public DynPythonEngine()
        {
            this.engine = Python.CreateEngine();
        }

        public void ProcessCode(string code)
        {
            code = "import clr\nclr.AddReference('RevitAPI')\nclr.AddReference('RevitAPIUI')\nfrom Autodesk.Revit.DB import *\n" + code;
            this.source = engine.CreateScriptSourceFromString(code, SourceCodeKind.Statements);
        }

        public Value Evaluate(IEnumerable<Binding> bindings)
        {
            var scope = this.engine.CreateScope();

            foreach (var bind in bindings)
            {
                scope.SetVariable(bind.Symbol, bind.Value);
            }

            try
            {
                this.source.Execute(scope);
            }
            catch (SyntaxErrorException ex)
            {
                throw new Exception(
                   ex.Message
                   + " at Line " + (ex.Line - 4)
                   + ", Column " + ex.Column
                );
            }

            Value result = Value.NewNumber(1);

            if (scope.ContainsVariable("OUT"))
            {
                dynamic output = scope.GetVariable("OUT");

                result = Converters.convertToValue(output);
            }

            return result;
        }
    }

    public struct Binding
    {
        public string Symbol;
        public dynamic Value;

        public Binding(string sym, dynamic val)
        {
            this.Symbol = sym;
            this.Value = val;
        }
    }

    public static class PythonBindings
    {
        static PythonBindings()
        {
            Bindings = new HashSet<Binding>();
            Bindings.Add(new Binding("__dynamo__", dynSettings.Controller));
        }

        public static HashSet<Binding> Bindings { get; private set; }
    }

    public static class PythonEngine
    {
        public delegate Value EvaluationDelegate(bool dirty, string script, IEnumerable<Binding> bindings);

        public static EvaluationDelegate Evaluator;
        
        private static DynPythonEngine engine = new DynPythonEngine();

        static PythonEngine()
        {
            Evaluator = delegate(bool dirty, string script, IEnumerable<Binding> bindings)
            {
                if (dirty)
                {
                    engine.ProcessCode(script);
                    dirty = false;
                }

                return engine.Evaluate(PythonBindings.Bindings.Concat(bindings));
            };
        }
    }

    [NodeName("Python Script")]
    [NodeCategory(BuiltinNodeCategories.SCRIPTING)]
    [NodeDescription("Runs an embedded IronPython script")]
    public class dynPython : dynNodeWithOneOutput
    {
        private bool dirty = true;
        private Dictionary<string, dynamic> stateDict = new Dictionary<string, dynamic>();

        //TextBox tb;
        string script;

        public dynPython()
        {
            //add an edit window option to the 
            //main context window
            System.Windows.Controls.MenuItem editWindowItem = new System.Windows.Controls.MenuItem();
            editWindowItem.Header = "Edit...";
            editWindowItem.IsCheckable = false;
            NodeUI.MainContextMenu.Items.Add(editWindowItem);
            editWindowItem.Click += new RoutedEventHandler(editWindowItem_Click);

            InPortData.Add(new PortData("IN", "Input", typeof(object)));
            OutPortData.Add(new PortData("OUT", "Result of the python script", typeof(object)));

            NodeUI.RegisterAllPorts();

            //topControl.Height = 200;
            //topControl.Width = 300;

            NodeUI.UpdateLayout();
        }

        //TODO: Make this smarter
        public override bool RequiresRecalc
        {
            get
            {
                return true;
            }
            set { }
        }

        public override void SaveElement(XmlDocument xmlDoc, XmlElement dynEl)
        {
            XmlElement script = xmlDoc.CreateElement("Script");
            //script.InnerText = this.tb.Text;
            script.InnerText = this.script;
            dynEl.AppendChild(script);
        }

        public override void LoadElement(XmlNode elNode)
        {
            foreach (XmlNode subNode in elNode.ChildNodes)
            {
                if (subNode.Name == "Script")
                    //this.tb.Text = subNode.InnerText;
                    script = subNode.InnerText;
            }
        }

        private List<Binding> makeBindings(IEnumerable<Value> args)
        {
            //Zip up our inputs
            var bindings = this.InPortData
               .Select(x => x.NickName)
               .Zip(args, (s, v) => new Binding(s, Converters.convertFromValue(v)))
               .Concat(PythonBindings.Bindings)
               .ToList();

            bindings.Add(new Binding("__persistant__", this.stateDict));

            return bindings;
        }

        public override Value Evaluate(FSharpList<Value> args)
        {
            return PythonEngine.Evaluator(dirty, script, makeBindings(args));
        }

        void editWindowItem_Click(object sender, RoutedEventArgs e)
        {
            dynEditWindow editWindow = new dynEditWindow();

            //set the text of the edit window to begin
            editWindow.editText.Text = script;

            if (editWindow.ShowDialog() != true)
            {
                return;
            }

            //set the value from the text in the box
            script = editWindow.editText.Text;

            this.dirty = true;
        }
    }

    [NodeName("Python Script From String")]
    [NodeCategory(BuiltinNodeCategories.SCRIPTING)]
    [NodeDescription("Runs a IronPython script from a string")]
    public class dynPythonString : dynNodeWithOneOutput
    {
        private DynPythonEngine engine = new DynPythonEngine();
        private Dictionary<string, dynamic> stateDict = new Dictionary<string, dynamic>();

        public dynPythonString()
        {
            InPortData.Add(new PortData("script", "Script to run", typeof(string)));
            InPortData.Add(new PortData("IN", "Input", typeof(object)));
            OutPortData.Add(new PortData("OUT", "Result of the python script", typeof(object)));

            NodeUI.RegisterAllPorts();
        }

        private List<Binding> makeBindings(IEnumerable<Value> args)
        {
            //Zip up our inputs
            var bindings = this.InPortData
               .Select(x => x.NickName)
               .Zip(args, (s, v) => new Binding(s, Converters.convertFromValue(v)))
               .Concat(PythonBindings.Bindings)
               .ToList();

            bindings.Add(new Binding("__persistant__", this.stateDict));

            return bindings;
        }

        public override Value Evaluate(FSharpList<Value> args)
        {
            return PythonEngine.Evaluator(
                RequiresRecalc, 
                ((Value.String)args[0]).Item, 
                makeBindings(args.Skip(1)));
        }
    }
}
