//Copyright 2013 Ian Keough

//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at

//http://www.apache.org/licenses/LICENSE-2.0

//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using System.Xml;
using System.Web;

using Dynamo.Connectors;
using Dynamo.Controls;
using Dynamo.FSchemeInterop;
using Dynamo.FSchemeInterop.Node;
using Dynamo.Utilities;

using Microsoft.FSharp.Collections;

using Value = Dynamo.FScheme.Value;
using TextBox = System.Windows.Controls.TextBox;
using System.Diagnostics.Contracts;
using System.Text;
using System.Windows.Input;
using System.Windows.Data;
using System.Globalization;

namespace Dynamo.Nodes
{
    /// <summary>
    /// Built-in Dynamo Categories. If you want your node to appear in one of the existing Dynamo
    /// categories, then use these constants. This ensures that if the names of the categories
    /// change down the road, your node will still be placed there.
    /// </summary>
    public static class BuiltinNodeCategories
    {
        public const string MATH = "Math";
        public const string COMPARISON = "Comparison";
        public const string BOOLEAN = "Logic";
        public const string PRIMITIVES = "Primitives";
        public const string REVIT = "Revit";
        public const string REVIT_XYZ_UV_VECTOR = "Revit XYZ UV Vector";
        public const string REVIT_TRANSFORMS = "Revit Transforms";
        public const string REVIT_POINTS = "Revit Points";
        public const string REVIT_GEOM = "Revit Geometry";
        public const string REVIT_CURVES = "Revit Model Curves";
        public const string REVIT_DATUMS = "Revit Datums";
        public const string COMMUNICATION = "Communication";
        public const string SCRIPTING = "Scripting";
        public const string STRINGS = "Strings";
        public const string MISC = "Miscellaneous";
        public const string FILES = "Files";
        public const string LIST = "Lists";
        public const string ANALYSIS = "Analysis";
        public const string MEASUREMENT = "Measurement";
        public const string TESSELLATION = "Tessellation";
        public const string DEBUG = "Debug";
        public const string SELECTION = "Selection";
        public const string EXECUTION = "Execution";
        public const string SIMULATION = "Simulation";
    }

    static class Utilities
    {
        public static string Ellipsis(string value, int desiredLength)
        {
            if (desiredLength > value.Length)
            {
                return value;
            }
            else
            {
                return value.Remove(desiredLength - 1) + "...";
            }
        }
    }

    #region FScheme Builtin Interop

    public abstract class dynBuiltinFunction : dynNodeWithOneOutput
    {
        public string Symbol;

        internal dynBuiltinFunction(string symbol)
        {
            Symbol = symbol;
        }

        protected override InputNode Compile(IEnumerable<string> portNames)
        {
            if (SaveResult)
            {
                return base.Compile(portNames);
            }
            else
                return new FunctionNode(Symbol, portNames);
        }

        public override Value Evaluate(FSharpList<Value> args)
        {
            return ((Value.Function)Controller.FSchemeEnvironment.LookupSymbol(Symbol))
                .Item.Invoke(args);
        }
    }

    #endregion

    public abstract class dynVariableInput : dynNodeWithOneOutput
    {
        protected dynVariableInput()
        {
            System.Windows.Controls.Button addButton = new System.Windows.Controls.Button();
            addButton.Content = "+";
            addButton.Width = 20;
            addButton.Height = 20;
            addButton.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            addButton.VerticalAlignment = System.Windows.VerticalAlignment.Bottom;

            System.Windows.Controls.Button subButton = new System.Windows.Controls.Button();
            subButton.Content = "-";
            subButton.Width = 20;
            subButton.Height = 20;
            subButton.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            subButton.VerticalAlignment = System.Windows.VerticalAlignment.Bottom;

            NodeUI.inputGrid.ColumnDefinitions.Add(new ColumnDefinition());
            NodeUI.inputGrid.ColumnDefinitions.Add(new ColumnDefinition());

            NodeUI.inputGrid.Children.Add(addButton);
            System.Windows.Controls.Grid.SetColumn(addButton, 0);

            NodeUI.inputGrid.Children.Add(subButton);
            System.Windows.Controls.Grid.SetColumn(subButton, 1);

            addButton.Click += delegate { AddInput(); NodeUI.RegisterAllPorts(); };
            subButton.Click += delegate { RemoveInput(); NodeUI.RegisterAllPorts(); };
        }

        protected abstract string getInputRootName();
        protected virtual int getNewInputIndex()
        {
            return InPortData.Count;
        }

        private int lastEvaledAmt;
        public override bool RequiresRecalc
        {
            get
            {
                return lastEvaledAmt != InPortData.Count || base.RequiresRecalc;
            }
            set
            {
                base.RequiresRecalc = value;
            }
        }

        protected internal virtual void RemoveInput()
        {
            var count = InPortData.Count;
            if (count > 0)
            {
                InPortData.RemoveAt(count - 1);
            }
        }

        protected internal virtual void AddInput()
        {
            InPortData.Add(new PortData(getInputRootName() + getNewInputIndex(), "", typeof(object)));
        }

        public override void SaveElement(XmlDocument xmlDoc, XmlElement dynEl)
        {
            //Debug.WriteLine(pd.Object.GetType().ToString());
            foreach (var inport in InPortData)
            {
                XmlElement input = xmlDoc.CreateElement("Input");

                input.SetAttribute("name", inport.NickName);

                dynEl.AppendChild(input);
            }
        }

        public override void LoadElement(XmlNode elNode)
        {
            int i = InPortData.Count;
            foreach (XmlNode subNode in elNode.ChildNodes)
            {
                if (i > 0)
                {
                    i--;
                    continue;
                }

                if (subNode.Name == "Input")
                {
                    InPortData.Add(new PortData(subNode.Attributes["name"].Value, "", typeof(object)));
                }
            }
            NodeUI.RegisterAllPorts();
        }

        protected override void OnEvaluate()
        {
            lastEvaledAmt = InPortData.Count;
        }
    }

    [NodeName("Identity")]
    [NodeCategory(BuiltinNodeCategories.MISC)]
    [NodeDescription("Identity function")]
    public class dynIdentity : dynNodeWithOneOutput
    {
        public dynIdentity()
        {
            InPortData.Add(new PortData("x", "in", typeof(bool)));
            OutPortData.Add(new PortData("x", "out", typeof(object)));
            NodeUI.RegisterAllPorts();
        }

        public override Value Evaluate(FSharpList<Value> args)
        {
            return args[0];
        }
    }

    #region Lists

    [NodeName("Reverse")]
    [NodeDescription("Reverses a list")]
    [NodeCategory(BuiltinNodeCategories.LIST)]
    public class dynReverse : dynBuiltinFunction
    {
        public dynReverse()
            : base("reverse")
        {
            InPortData.Add(new PortData("list", "List to sort", typeof(object)));
            OutPortData.Add(new PortData("rev", "Reversed list", typeof(object)));

            NodeUI.RegisterAllPorts();
        }
    }

    [NodeName("List")]
    [NodeDescription("Makes a new list out of the given inputs")]
    [NodeCategory(BuiltinNodeCategories.LIST)]
    public class dynNewList : dynVariableInput
    {
        public dynNewList()
        {
            InPortData.Add(new PortData("item(s)", "Item(s) to build a list out of", typeof(object)));
            OutPortData.Add(new PortData("list", "A list", typeof(object)));

            NodeUI.RegisterAllPorts();
        }

        protected override string getInputRootName()
        {
            return "index";
        }

        protected internal override void RemoveInput()
        {
            if (InPortData.Count == 2)
                InPortData[0] = new PortData("item(s)", "Item(s) to build a list out of", typeof(object));
            if (InPortData.Count > 1)
                base.RemoveInput();
        }

        protected internal override void AddInput()
        {
            if (InPortData.Count == 1)
                InPortData[0] = new PortData("index0", "First item", typeof(object));
            base.AddInput();
        }

        protected override InputNode Compile(IEnumerable<string> portNames)
        {
            if (SaveResult)
                return base.Compile(portNames);
            else
                return new FunctionNode("list", portNames);
        }

        public override Value Evaluate(FSharpList<Value> args)
        {
            return ((Value.Function)Controller.FSchemeEnvironment.LookupSymbol("list"))
                .Item.Invoke(args);
        }
    }

    [NodeName("Sort-With")]
    [NodeCategory(BuiltinNodeCategories.LIST)]
    [NodeDescription("Returns a sorted list, using the given comparitor.")]
    public class dynSortWith : dynBuiltinFunction
    {
        public dynSortWith()
            : base("sort-with")
        {
            InPortData.Add(new PortData("list", "List to sort", typeof(object)));
            InPortData.Add(new PortData("c(x, y)", "Comparitor", typeof(object)));
            OutPortData.Add(new PortData("sorted", "Sorted list", typeof(object)));

            NodeUI.RegisterAllPorts();
        }
    }

    [NodeName("Sort-By")]
    [NodeCategory(BuiltinNodeCategories.LIST)]
    [NodeDescription("Returns a sorted list, using the given key mapper.")]
    public class dynSortBy : dynBuiltinFunction
    {
        public dynSortBy()
            : base("sort-by")
        {
            InPortData.Add(new PortData("list", "List to sort", typeof(object)));
            InPortData.Add(new PortData("c(x)", "Key Mapper", typeof(object)));
            OutPortData.Add(new PortData("sorted", "Sorted list", typeof(object)));

            NodeUI.RegisterAllPorts();
        }
    }

    [NodeName("Sort")]
    [NodeCategory(BuiltinNodeCategories.LIST)]
    [NodeDescription("Returns a sorted list of numbers or strings.")]
    public class dynSort : dynBuiltinFunction
    {
        public dynSort()
            : base("sort")
        {
            InPortData.Add(new PortData("list", "List of numbers or strings to sort", typeof(object)));
            OutPortData.Add(new PortData("sorted", "Sorted list", typeof(object)));

            NodeUI.RegisterAllPorts();
        }
    }

    [NodeName("Reduce")]
    [NodeCategory(BuiltinNodeCategories.LIST)]
    [NodeDescription("Reduces a sequence.")]
    [NodeSearchTags("foldl")]
    public class dynFold : dynBuiltinFunction
    {
        public dynFold()
            : base("foldl")
        {
            InPortData.Add(new PortData("f(x, a)", "Reductor Funtion", typeof(object)));
            InPortData.Add(new PortData("a", "Seed", typeof(object)));
            InPortData.Add(new PortData("seq", "Sequence", typeof(object)));
            OutPortData.Add(new PortData("out", "Result", typeof(object)));

            NodeUI.RegisterAllPorts();
        }
    }

    [NodeName("Filter")]
    [NodeCategory(BuiltinNodeCategories.LIST)]
    [NodeDescription("Filters a sequence by a given predicate")]
    public class dynFilter : dynBuiltinFunction
    {
        public dynFilter()
            : base("filter")
        {
            InPortData.Add(new PortData("p(x)", "Predicate", typeof(object)));
            InPortData.Add(new PortData("seq", "Sequence to filter", typeof(object)));
            OutPortData.Add(new PortData("filtered", "Filtered Sequence", typeof(object)));

            NodeUI.RegisterAllPorts();
        }
    }

    [NodeName("Number Sequence")]
    [NodeCategory(BuiltinNodeCategories.LIST)]
    [NodeDescription("Creates a sequence of numbers")]
    [NodeSearchTags("range")]
    public class dynBuildSeq : dynBuiltinFunction
    {
        public dynBuildSeq()
            : base("build-list")
        {
            InPortData.Add(new PortData("start", "Number to start the sequence at", typeof(double)));
            InPortData.Add(new PortData("end", "Number to end the sequence at", typeof(double)));
            InPortData.Add(new PortData("step", "Space between numbers", typeof(double)));
            OutPortData.Add(new PortData("seq", "New sequence", typeof(object)));

            NodeUI.RegisterAllPorts();
        }
    }

    [NodeName("Combine")]
    [NodeCategory(BuiltinNodeCategories.LIST)]
    [NodeDescription("Applies a combinator to each element in two sequences")]
    [NodeSearchTags("zip")]
    public class dynCombine : dynVariableInput
    {
        public dynCombine()
        {
            InPortData.Add(new PortData("comb", "Combinator", typeof(object)));
            InPortData.Add(new PortData("list1", "First list", typeof(object)));
            InPortData.Add(new PortData("list2", "Second list", typeof(object)));
            OutPortData.Add(new PortData("combined", "Combined lists", typeof(object)));

            NodeUI.RegisterAllPorts();
        }

        protected override string getInputRootName()
        {
            return "list";
        }

        protected internal override void RemoveInput()
        {
            if (InPortData.Count == 3)
                InPortData[1] = new PortData("lists", "List of lists to combine", typeof(object));
            if (InPortData.Count > 2)
                base.RemoveInput();
        }

        protected internal override void AddInput()
        {
            if (InPortData.Count == 2)
                InPortData[1] = new PortData("list1", "First list", typeof(object));
            base.AddInput();
        }

        public override void SaveElement(XmlDocument xmlDoc, XmlElement dynEl)
        {
            dynEl.SetAttribute("inputs", (InPortData.Count - 1).ToString());
        }

        public override void LoadElement(XmlNode elNode)
        {
            var inputAttr = elNode.Attributes["inputs"];
            int inputs = inputAttr == null ? 2 : Convert.ToInt32(inputAttr.Value);
            if (inputs == 1)
                RemoveInput();
            else
            {
                for (; inputs > 2; inputs--)
                {
                    InPortData.Add(new PortData(getInputRootName() + getNewInputIndex(), "", typeof(object)));
                }

                NodeUI.RegisterAllPorts();
            }
        }

        protected override InputNode Compile(IEnumerable<string> portNames)
        {
            if (SaveResult)
            {
                return base.Compile(portNames);
            }
            else
                return new FunctionNode("map", portNames);
        }

        public override Value Evaluate(FSharpList<Value> args)
        {
            return ((Value.Function)Controller.FSchemeEnvironment.LookupSymbol("map"))
                .Item.Invoke(args);
        }
    }

    [NodeName("Cartesian Product")]
    [NodeCategory(BuiltinNodeCategories.LIST)]
    [NodeDescription("Applies a combinator to each pair in the cartesian product of two sequences")]
    [NodeSearchTags("cross")]
    public class dynCartProd : dynVariableInput
    {
        public dynCartProd()
        {
            InPortData.Add(new PortData("comb", "Combinator", typeof(object)));
            InPortData.Add(new PortData("list1", "First list", typeof(object)));
            InPortData.Add(new PortData("list2", "Second list", typeof(object)));
            OutPortData.Add(new PortData("combined", "Combined lists", typeof(object)));

            NodeUI.RegisterAllPorts();
        }

        protected override string getInputRootName()
        {
            return "list";
        }

        protected internal override void RemoveInput()
        {
            if (InPortData.Count == 3)
                InPortData[1] = new PortData("lists", "List of lists to combine", typeof(object));
            if (InPortData.Count > 2)
                base.RemoveInput();
        }

        protected internal override void AddInput()
        {
            if (InPortData.Count == 2)
                InPortData[1] = new PortData("list1", "First list", typeof(object));
            base.AddInput();
        }

        public override void SaveElement(XmlDocument xmlDoc, XmlElement dynEl)
        {
            dynEl.SetAttribute("inputs", (InPortData.Count - 1).ToString());
        }

        public override void LoadElement(XmlNode elNode)
        {
            var inputAttr = elNode.Attributes["inputs"];
            int inputs = inputAttr == null ? 2 : Convert.ToInt32(inputAttr.Value);
            if (inputs == 1)
                RemoveInput();
            else
            {
                for (; inputs > 2; inputs--)
                {
                    InPortData.Add(new PortData(getInputRootName() + getNewInputIndex(), "", typeof(object)));
                }

                NodeUI.RegisterAllPorts();
            }
        }

        protected override InputNode Compile(IEnumerable<string> portNames)
        {
            if (SaveResult)
            {
                return base.Compile(portNames);
            }
            else
                return new FunctionNode("cartesian-product", portNames);
        }

        public override Value Evaluate(FSharpList<Value> args)
        {
            return ((Value.Function)Controller.FSchemeEnvironment.LookupSymbol("cartesian-product"))
                .Item.Invoke(args);
        }
    }

    [NodeName("Map")]
    [NodeCategory(BuiltinNodeCategories.LIST)]
    [NodeDescription("Maps a sequence")]
    public class dynMap : dynBuiltinFunction
    {
        public dynMap()
            : base("map")
        {
            InPortData.Add(new PortData("f(x)", "The procedure used to map elements", typeof(object)));
            InPortData.Add(new PortData("seq", "The sequence to map over.", typeof(object)));
            OutPortData.Add(new PortData("mapped", "Mapped sequence", typeof(object)));

            NodeUI.RegisterAllPorts();
        }
    }

    [NodeName("De-Cons")]
    [NodeCategory(BuiltinNodeCategories.LIST)]
    [NodeDescription("Deconstructs a list pair.")]
    public class dynDeCons : dynNode
    {
        public dynDeCons()
        {
            InPortData.Add(new PortData("list", "", typeof(object)));
            OutPortData.Add(new PortData("first", "", typeof(object)));
            OutPortData.Add(new PortData("rest", "", typeof(object)));

            NodeUI.RegisterAllPorts();
        }

        public override void Evaluate(FSharpList<Value> args, Dictionary<PortData, Value> outPuts)
        {
            var list = (Value.List)args[0];

            outPuts[OutPortData[0]] = list.Item.Head;
            outPuts[OutPortData[1]] = Value.NewList(list.Item.Tail);
        }
    }

    [NodeName("Cons")]
    [NodeCategory(BuiltinNodeCategories.LIST)]
    [NodeDescription("Constructs a list pair.")]
    public class dynList : dynBuiltinFunction
    {
        public dynList()
            : base("cons")
        {
            InPortData.Add(new PortData("first", "The new Head of the list", typeof(object)));
            InPortData.Add(new PortData("rest", "The new Tail of the list", typeof(object)));
            OutPortData.Add(new PortData("list", "Result List", typeof(object)));

            NodeUI.RegisterAllPorts();
        }
    }

    [NodeName("Take")]
    [NodeCategory(BuiltinNodeCategories.LIST)]
    [NodeDescription("Takes elements from a list")]
    public class dynTakeList : dynBuiltinFunction
    {
        public dynTakeList()
            : base("take")
        {
            InPortData.Add(new PortData("amt", "Amount of elements to extract", typeof(object)));
            InPortData.Add(new PortData("list", "The list to extract elements from", typeof(object)));
            OutPortData.Add(new PortData("elements", "List of extraced elements", typeof(object)));

            NodeUI.RegisterAllPorts();
        }
    }

    [NodeName("Drop")]
    [NodeCategory(BuiltinNodeCategories.LIST)]
    [NodeDescription("Drops elements from a list")]
    public class dynDropList : dynBuiltinFunction
    {
        public dynDropList()
            : base("drop")
        {
            InPortData.Add(new PortData("amt", "Amount of elements to drop", typeof(object)));
            InPortData.Add(new PortData("list", "The list to drop elements from", typeof(object)));
            OutPortData.Add(new PortData("elements", "List of remaining elements", typeof(object)));

            NodeUI.RegisterAllPorts();
        }
    }

    [NodeName("Get")]
    [NodeCategory(BuiltinNodeCategories.LIST)]
    [NodeDescription("Gets an element from a list at a specified index.")]
    public class dynGetFromList : dynBuiltinFunction
    {
        public dynGetFromList()
            : base("get")
        {
            InPortData.Add(new PortData("index", "Index of the element to extract", typeof(object)));
            InPortData.Add(new PortData("list", "The list to extract elements from", typeof(object)));
            OutPortData.Add(new PortData("element", "Extracted element", typeof(object)));

            NodeUI.RegisterAllPorts();
        }
    }

    [NodeName("Empty")]
    [NodeCategory(BuiltinNodeCategories.LIST)]
    [NodeDescription("An empty list")]
    [IsInteractive(false)]
    public class dynEmpty : dynNodeWithOneOutput
    {
        public dynEmpty()
        {
            OutPortData.Add(new PortData("empty", "An empty list", typeof(object)));

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

        public override Value Evaluate(FSharpList<Value> args)
        {
            return Value.NewList(FSharpList<Value>.Empty);
        }

        protected internal override INode Build(Dictionary<dynNode, Dictionary<int, INode>> preBuilt, int outPort)
        {
            Dictionary<int, INode> result;
            if (!preBuilt.TryGetValue(this, out result))
            {
                result = new Dictionary<int, INode>();
                result[outPort] = new SymbolNode("empty");
                preBuilt[this] = result;
            }
            return result[outPort];
        }
    }

    [NodeName("Is Empty?")]
    [NodeCategory(BuiltinNodeCategories.LIST)]
    [NodeDescription("Checks to see if the given list is empty.")]
    public class dynIsEmpty : dynBuiltinFunction
    {
        public dynIsEmpty()
            : base("empty?")
        {
            InPortData.Add(new PortData("list", "A list", typeof(object)));
            OutPortData.Add(new PortData("empty?", "Is the given list empty?", typeof(bool)));

            NodeUI.RegisterAllPorts();
        }
    }

    [NodeName("Length")]
    [NodeCategory(BuiltinNodeCategories.LIST)]
    [NodeDescription("Gets the length of a list")]
    [NodeSearchTags("count")]
    public class dynLength : dynBuiltinFunction
    {
        public dynLength()
            : base("len")
        {
            InPortData.Add(new PortData("list", "A list", typeof(object)));
            OutPortData.Add(new PortData("length", "Length of the list", typeof(object)));

            NodeUI.RegisterAllPorts();
        }
    }

    [NodeName("Append")]
    [NodeCategory(BuiltinNodeCategories.LIST)]
    [NodeDescription("Appends two list")]
    public class dynAppend : dynBuiltinFunction
    {
        public dynAppend()
            : base("append")
        {
            InPortData.Add(new PortData("listA", "First list", typeof(object)));
            InPortData.Add(new PortData("listB", "Second list", typeof(object)));
            OutPortData.Add(new PortData("A+B", "A appended onto B", typeof(object)));

            NodeUI.RegisterAllPorts();
        }
    }

    [NodeName("First")]
    [NodeCategory(BuiltinNodeCategories.LIST)]
    [NodeDescription("Gets the first element of a list")]
    public class dynFirst : dynBuiltinFunction
    {
        public dynFirst()
            : base("first")
        {
            InPortData.Add(new PortData("list", "A list", typeof(object)));
            OutPortData.Add(new PortData("first", "First element in the list", typeof(object)));

            NodeUI.RegisterAllPorts();
        }
    }

    [NodeName("Rest")]
    [NodeCategory(BuiltinNodeCategories.LIST)]
    [NodeDescription("Gets the list with the first element removed.")]
    public class dynRest : dynBuiltinFunction
    {
        public dynRest()
            : base("rest")
        {
            InPortData.Add(new PortData("list", "A list", typeof(object)));
            OutPortData.Add(new PortData("rest", "List without the first element.", typeof(object)));

            NodeUI.RegisterAllPorts();
        }
    }

    #endregion

    #region Boolean

    public abstract class dynComparison : dynBuiltinFunction
    {
        protected dynComparison(string op) : this(op, op) { }

        protected dynComparison(string op, string name)
            : base(op)
        {
            InPortData.Add(new PortData("x", "operand", typeof(double)));
            InPortData.Add(new PortData("y", "operand", typeof(double)));
            OutPortData.Add(new PortData("x" + name + "y", "comp", typeof(double)));

            NodeUI.nickNameBlock.FontSize = 20;

            NodeUI.RegisterAllPorts();
        }
    }

    [NodeName("<")]
    [NodeCategory(BuiltinNodeCategories.COMPARISON)]
    [NodeDescription("Compares two numbers.")]
    [NodeSearchTags("less", "than")]
    public class dynLessThan : dynComparison
    {
        public dynLessThan() : base("<") { }
    }

    [NodeName("≤")]
    [NodeCategory(BuiltinNodeCategories.COMPARISON)]
    [NodeDescription("Compares two numbers.")]
    [NodeSearchTags("<=", "less", "than", "equal")]
    public class dynLessThanEquals : dynComparison
    {
        public dynLessThanEquals() : base("<=", "≤") { }
    }

    [NodeName(">")]
    [NodeCategory(BuiltinNodeCategories.COMPARISON)]
    [NodeDescription("Compares two numbers.")]
    [NodeSearchTags("greater", "than")]
    public class dynGreaterThan : dynComparison
    {
        public dynGreaterThan() : base(">") { }
    }

    [NodeName("≥")]
    [NodeCategory(BuiltinNodeCategories.COMPARISON)]
    [NodeDescription("Compares two numbers.")]
    [NodeSearchTags(">=", "greater", "than", "equal")]
    public class dynGreaterThanEquals : dynComparison
    {
        public dynGreaterThanEquals() : base(">=", "≥") { }
    }

    [NodeName("=")]
    [NodeCategory(BuiltinNodeCategories.COMPARISON)]
    [NodeDescription("Compares two numbers.")]
    public class dynEqual : dynComparison
    {
        public dynEqual() : base("=") { }
    }

    [NodeName("And")]
    [NodeCategory(BuiltinNodeCategories.BOOLEAN)]
    [NodeDescription("Boolean AND.")]
    public class dynAnd : dynBuiltinFunction
    {
        public dynAnd()
            : base("and")
        {
            InPortData.Add(new PortData("a", "operand", typeof(double)));
            InPortData.Add(new PortData("b", "operand", typeof(double)));
            OutPortData.Add(new PortData("a∧b", "result", typeof(double)));

            NodeUI.nickNameBlock.FontSize = 20;

            NodeUI.RegisterAllPorts();
        }

        protected internal override INode Build(Dictionary<dynNode, Dictionary<int, INode>> preBuilt, int outPort)
        {
            Dictionary<int, INode> result;
            if (!preBuilt.TryGetValue(this, out result))
            {
                if (Enumerable.Range(0, InPortData.Count).All(HasInput))
                {
                    var ifNode = new ConditionalNode();
                    ifNode.ConnectInput("test", Inputs[0].Item2.Build(preBuilt, Inputs[0].Item1));
                    ifNode.ConnectInput("true", Inputs[1].Item2.Build(preBuilt, Inputs[1].Item1));
                    ifNode.ConnectInput("false", new NumberNode(0));
                    result = new Dictionary<int, INode>();
                    result[outPort] = ifNode;
                }
                else
                {
                    var ifNode = new ConditionalNode();
                    ifNode.ConnectInput("test", new SymbolNode(InPortData[0].NickName));
                    ifNode.ConnectInput("true", new SymbolNode(InPortData[1].NickName));
                    ifNode.ConnectInput("false", new NumberNode(0));

                    var node = new AnonymousFunctionNode(
                        InPortData.Select(x => x.NickName),
                        ifNode);

                    //For each index in InPortData
                    //for (int i = 0; i < InPortData.Count; i++)
                    foreach (var data in Enumerable.Range(0, InPortData.Count))
                    {
                        //Fetch the corresponding port
                        //var port = InPorts[i];

                        //If this port has connectors...
                        //if (port.Connectors.Any())
                        if (HasInput(data))
                        {
                            //Compile input and connect it
                            node.ConnectInput(
                               InPortData[data].NickName,
                               Inputs[data].Item2.Build(preBuilt, Inputs[data].Item1)
                            );
                        }
                    }

                    RequiresRecalc = false;
                    OnEvaluate();

                    result = new Dictionary<int, INode>();
                    result[outPort] = node;
                }
                preBuilt[this] = result;
            }
            return result[outPort];
        }
    }

    [NodeName("Or")]
    [NodeCategory(BuiltinNodeCategories.BOOLEAN)]
    [NodeDescription("Boolean OR.")]
    public class dynOr : dynBuiltinFunction
    {
        public dynOr()
            : base("or")
        {
            InPortData.Add(new PortData("a", "operand", typeof(bool)));
            InPortData.Add(new PortData("b", "operand", typeof(bool)));
            OutPortData.Add(new PortData("a∨b", "result", typeof(bool)));

            NodeUI.nickNameBlock.FontSize = 20;

            NodeUI.RegisterAllPorts();
        }

        protected internal override INode Build(Dictionary<dynNode, Dictionary<int, INode>> preBuilt, int outPort)
        {
            Dictionary<int, INode> result;
            if (!preBuilt.TryGetValue(this, out result))
            {
                if (Enumerable.Range(0, InPortData.Count).All(HasInput))
                {
                    var ifNode = new ConditionalNode();
                    ifNode.ConnectInput("test", Inputs[0].Item2.Build(preBuilt, Inputs[0].Item1));
                    ifNode.ConnectInput("true", new NumberNode(1));
                    ifNode.ConnectInput("false", Inputs[1].Item2.Build(preBuilt, Inputs[1].Item1));

                    result = new Dictionary<int, INode>();
                    result[outPort] = ifNode;
                }
                else
                {
                    var ifNode = new ConditionalNode();
                    ifNode.ConnectInput("test", new SymbolNode(InPortData[0].NickName));
                    ifNode.ConnectInput("true", new NumberNode(1));
                    ifNode.ConnectInput("false", new SymbolNode(InPortData[1].NickName));

                    var node = new AnonymousFunctionNode(
                        InPortData.Select(x => x.NickName),
                        ifNode);

                    //For each index in InPortData
                    //for (int i = 0; i < InPortData.Count; i++)
                    foreach (var data in Enumerable.Range(0, InPortData.Count))
                    {
                        //Fetch the corresponding port
                        //var port = InPorts[i];

                        //If this port has connectors...
                        //if (port.Connectors.Any())
                        if (HasInput(data))
                        {
                            //Compile input and connect it
                            node.ConnectInput(
                               InPortData[data].NickName,
                               Inputs[data].Item2.Build(preBuilt, Inputs[data].Item1)
                            );
                        }
                    }

                    RequiresRecalc = false;
                    OnEvaluate();

                    result = new Dictionary<int, INode>();
                    result[outPort] = node;
                }
                preBuilt[this] = result;
            }
            return result[outPort];
        }
    }

    [NodeName("Xor")]
    [NodeCategory(BuiltinNodeCategories.BOOLEAN)]
    [NodeDescription("Boolean XOR.")]
    public class dynXor : dynBuiltinFunction
    {
        public dynXor()
            : base("xor")
        {
            InPortData.Add(new PortData("a", "operand", typeof(bool)));
            InPortData.Add(new PortData("b", "operand", typeof(bool)));
            OutPortData.Add(new PortData("a⊻b", "result", typeof(bool)));

            NodeUI.nickNameBlock.FontSize = 20;

            NodeUI.RegisterAllPorts();
        }
    }

    [NodeName("Not")]
    [NodeCategory(BuiltinNodeCategories.BOOLEAN)]
    [NodeDescription("Boolean NOT.")]
    public class dynNot : dynBuiltinFunction
    {
        public dynNot()
            : base("not")
        {
            InPortData.Add(new PortData("a", "operand", typeof(bool)));
            OutPortData.Add(new PortData("!a", "result", typeof(bool)));

            NodeUI.nickNameBlock.FontSize = 20;

            NodeUI.RegisterAllPorts();
        }
    }

    #endregion

    #region Math

    [NodeName("+")]
    [NodeCategory(BuiltinNodeCategories.MATH)]
    [NodeDescription("Adds two numbers.")]
    [NodeSearchTags("plus", "addition", "sum")]
    public class dynAddition : dynBuiltinFunction
    {
        public dynAddition()
            : base("+")
        {
            InPortData.Add(new PortData("x", "operand", typeof(double)));
            InPortData.Add(new PortData("y", "operand", typeof(double)));
            OutPortData.Add(new PortData("x+y", "sum", typeof(double)));

            NodeUI.nickNameBlock.FontSize = 20;

            NodeUI.RegisterAllPorts();
        }
    }

    [NodeName("−")]
    [NodeCategory(BuiltinNodeCategories.MATH)]
    [NodeDescription("Subtracts two numbers.")]
    [NodeSearchTags("subtraction", "minus", "difference", "-")]
    public class dynSubtraction : dynBuiltinFunction
    {
        public dynSubtraction()
            : base("-")
        {
            InPortData.Add(new PortData("x", "operand", typeof(double)));
            InPortData.Add(new PortData("y", "operand", typeof(double)));
            OutPortData.Add(new PortData("x-y", "difference", typeof(double)));

            NodeUI.nickNameBlock.FontSize = 20;

            NodeUI.RegisterAllPorts();
        }
    }

    [NodeName("×")]
    [NodeCategory(BuiltinNodeCategories.MATH)]
    [NodeDescription("Multiplies two numbers.")]
    [NodeSearchTags("times", "multiply", "multiplication", "product", "*", "x")]
    public class dynMultiplication : dynBuiltinFunction
    {
        public dynMultiplication()
            : base("*")
        {
            InPortData.Add(new PortData("x", "operand", typeof(double)));
            InPortData.Add(new PortData("y", "operand", typeof(double)));
            OutPortData.Add(new PortData("x∙y", "product", typeof(double)));

            NodeUI.nickNameBlock.FontSize = 20;

            NodeUI.RegisterAllPorts();
        }
    }

    [NodeName("÷")]
    [NodeCategory(BuiltinNodeCategories.MATH)]
    [NodeDescription("Divides two numbers.")]
    [NodeSearchTags("divide", "division", "quotient", "/")]
    public class dynDivision : dynBuiltinFunction
    {
        public dynDivision()
            : base("/")
        {
            InPortData.Add(new PortData("x", "operand", typeof(double)));
            InPortData.Add(new PortData("y", "operand", typeof(double)));
            OutPortData.Add(new PortData("x÷y", "result", typeof(double)));

            NodeUI.nickNameBlock.FontSize = 20;

            NodeUI.RegisterAllPorts();
        }
    }

    [NodeName("Mod")]
    [NodeCategory(BuiltinNodeCategories.MATH)]
    [NodeDescription("Remainder of division of two numbers.")]
    [NodeSearchTags("%", "modulo", "remainder")]
    public class dynModulo : dynBuiltinFunction
    {
        public dynModulo()
            : base("%")
        {
            InPortData.Add(new PortData("x", "operand", typeof(double)));
            InPortData.Add(new PortData("y", "operand", typeof(double)));
            OutPortData.Add(new PortData("x%y", "result", typeof(double)));

            NodeUI.RegisterAllPorts();
        }
    }

    [NodeName("Pow")]
    [NodeCategory(BuiltinNodeCategories.MATH)]
    [NodeDescription("Raises a number to the power of another.")]
    [NodeSearchTags("power", "exponentiation", "^")]
    public class dynPow : dynBuiltinFunction
    {
        public dynPow()
            : base("pow")
        {
            InPortData.Add(new PortData("x", "operand", typeof(double)));
            InPortData.Add(new PortData("y", "operand", typeof(double)));
            OutPortData.Add(new PortData("x^y", "result", typeof(double)));

            NodeUI.RegisterAllPorts();
        }
    }

    [NodeName("Round")]
    [NodeCategory(BuiltinNodeCategories.MATH)]
    [NodeDescription("Rounds a number to the nearest integer value.")]
    public class dynRound : dynNodeWithOneOutput
    {
        public dynRound()
        {
            InPortData.Add(new PortData("dbl", "A number", typeof(double)));
            OutPortData.Add(new PortData("int", "Rounded number", typeof(double)));

            NodeUI.RegisterAllPorts();
        }

        public override Value Evaluate(FSharpList<Value> args)
        {
            return Value.NewNumber(
               Math.Round(((Value.Number)args[0]).Item)
            );
        }
    }

    [NodeName("Floor")]
    [NodeCategory(BuiltinNodeCategories.MATH)]
    [NodeDescription("Rounds a number to the nearest smaller integer.")]
    [NodeSearchTags("round")]
    public class dynFloor : dynNodeWithOneOutput
    {
        public dynFloor()
        {
            InPortData.Add(new PortData("dbl", "A number", typeof(double)));
            OutPortData.Add(new PortData("int", "Number rounded down", typeof(double)));

            NodeUI.RegisterAllPorts();
        }

        public override Value Evaluate(FSharpList<Value> args)
        {
            return Value.NewNumber(
               Math.Floor(((Value.Number)args[0]).Item)
            );
        }
    }

    [NodeName("Ceiling")]
    [NodeCategory(BuiltinNodeCategories.MATH)]
    [NodeDescription("Rounds a number to the nearest larger integer value.")]
    [NodeSearchTags("round")]
    public class dynCeiling : dynNodeWithOneOutput
    {
        public dynCeiling()
        {
            InPortData.Add(new PortData("dbl", "A number", typeof(double)));
            OutPortData.Add(new PortData("int", "Number rounded up", typeof(double)));

            NodeUI.RegisterAllPorts();
        }

        public override Value Evaluate(FSharpList<Value> args)
        {
            return Value.NewNumber(
               Math.Ceiling(((Value.Number)args[0]).Item)
            );
        }
    }

    [NodeName("Random")]
    [NodeCategory(BuiltinNodeCategories.MATH)]
    [NodeDescription("Generates a uniform random number in the range [0.0, 1.0).")]
    public class dynRandom : dynNodeWithOneOutput
    {
        public dynRandom()
        {
            OutPortData.Add(new PortData("rand", "Random number between 0.0 and 1.0.", typeof(double)));
            NodeUI.RegisterAllPorts();
        }

        private static Random random = new Random();

        public override bool RequiresRecalc
        {
            get
            {
                return true;
            }
            set { }
        }

        public override Value Evaluate(FSharpList<Value> args)
        {
            return Value.NewNumber(random.NextDouble());
        }
    }

    [NodeName("π")]
    [NodeCategory(BuiltinNodeCategories.MATH)]
    [NodeDescription("Pi constant")]
    [NodeSearchTags("pi", "trigonometry", "circle")]
    [IsInteractive(false)]
    public class dynPi : dynNode
    {
        public dynPi()
        {
            OutPortData.Add(new PortData("3.14159...", "pi", typeof(double)));

            NodeUI.nickNameBlock.FontSize = 20;

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

        protected internal override INode Build(Dictionary<dynNode, Dictionary<int, INode>> preBuilt, int outPort)
        {
            Dictionary<int, INode> result;
            if (!preBuilt.TryGetValue(this, out result))
            {
                result = new Dictionary<int, INode>();
                result[outPort] = new NumberNode(Math.PI);
                preBuilt[this] = result;
            }
            return result[outPort];
        }
    }

    [NodeName("Sine")]
    [NodeCategory(BuiltinNodeCategories.MATH)]
    [NodeDescription("Computes the sine of the given angle.")]
    public class dynSin : dynNodeWithOneOutput
    {
        public dynSin()
        {
            InPortData.Add(new PortData("θ", "Angle in radians", typeof(double)));
            OutPortData.Add(new PortData("sin(θ)", "Sine value of the given angle", typeof(double)));

            NodeUI.RegisterAllPorts();
        }

        public override Value Evaluate(FSharpList<Value> args)
        {
            var input = args[0];

            if (input.IsList)
            {
                return Value.NewList(
                   FSchemeInterop.Utils.SequenceToFSharpList(
                      ((Value.List)input).Item.Select(
                         x =>
                            Value.NewNumber(Math.Sin(((Value.Number)x).Item))
                      )
                   )
                );
            }
            else
            {
                double theta = ((Value.Number)input).Item;
                return Value.NewNumber(Math.Sin(theta));
            }
        }
    }

    [NodeName("Cosine")]
    [NodeCategory(BuiltinNodeCategories.MATH)]
    [NodeDescription("Computes the cosine of the given angle.")]
    public class dynCos : dynNodeWithOneOutput
    {
        public dynCos()
        {
            InPortData.Add(new PortData("θ", "Angle in radians", typeof(double)));
            OutPortData.Add(new PortData("cos(θ)", "Cosine value of the given angle", typeof(double)));

            NodeUI.RegisterAllPorts();
        }

        public override Value Evaluate(FSharpList<Value> args)
        {
            var input = args[0];

            if (input.IsList)
            {
                return Value.NewList(
                   FSchemeInterop.Utils.SequenceToFSharpList(
                      ((Value.List)input).Item.Select(
                         x =>
                            Value.NewNumber(Math.Cos(((Value.Number)x).Item))
                      )
                   )
                );
            }
            else
            {
                double theta = ((Value.Number)input).Item;
                return Value.NewNumber(Math.Cos(theta));
            }
        }
    }

    [NodeName("Tangent")]
    [NodeCategory(BuiltinNodeCategories.MATH)]
    [NodeDescription("Computes the tangent of the given angle.")]
    public class dynTan : dynNodeWithOneOutput
    {
        public dynTan()
        {
            InPortData.Add(new PortData("θ", "Angle in radians", typeof(double)));
            OutPortData.Add(new PortData("tan(θ)", "Tangent value of the given angle", typeof(double)));

            NodeUI.RegisterAllPorts();
        }

        public override Value Evaluate(FSharpList<Value> args)
        {
            var input = args[0];

            if (input.IsList)
            {
                return Value.NewList(
                   FSchemeInterop.Utils.SequenceToFSharpList(
                      ((Value.List)input).Item.Select(
                         x =>
                            Value.NewNumber(Math.Tan(((Value.Number)x).Item))
                      )
                   )
                );
            }
            else
            {
                double theta = ((Value.Number)input).Item;
                return Value.NewNumber(Math.Tan(theta));
            }
        }
    }

    #endregion

    #region Control Flow

    //TODO: Setup proper IsDirty smart execution management
    [NodeName("Perform All")]
    [NodeCategory(BuiltinNodeCategories.MISC)]
    [NodeDescription("Executes Values in a sequence")]
    [NodeSearchTags("begin")]
    public class dynBegin : dynVariableInput
    {
        public dynBegin()
        {
            InPortData.Add(new PortData("expr1", "Expression #1", typeof(object)));
            InPortData.Add(new PortData("expr2", "Expression #2", typeof(object)));
            OutPortData.Add(new PortData("last", "Result of final expression", typeof(object)));

            NodeUI.RegisterAllPorts();
        }

        protected internal override void RemoveInput()
        {
            if (InPortData.Count > 2)
                base.RemoveInput();
        }

        protected override string getInputRootName()
        {
            return "expr";
        }

        protected override int getNewInputIndex()
        {
            return InPortData.Count + 1;
        }

        private INode nestedBegins(Stack<Tuple<int, dynNode>> inputs, Dictionary<dynNode, Dictionary<int, INode>> preBuilt)
        {
            var popped = inputs.Pop();
            var firstVal = popped.Item2.Build(preBuilt, popped.Item1);

            if (inputs.Any())
            {
                var newBegin = new BeginNode();
                newBegin.ConnectInput("expr1", nestedBegins(inputs, preBuilt));
                newBegin.ConnectInput("expr2", firstVal);
                return newBegin;
            }
            else
                return firstVal;
        }

        protected internal override INode Build(Dictionary<dynNode, Dictionary<int, INode>> preBuilt, int outPort)
        {
            if (!Enumerable.Range(0, InPortData.Count).All(HasInput))
            {
                NodeUI.Error("All inputs must be connected.");
                throw new Exception("Begin Node requires all inputs to be connected.");
            }
            
            Dictionary<int, INode> result;
            if (!preBuilt.TryGetValue(this, out result))
            {
                result = new Dictionary<int, INode>(); 
                result[outPort] = 
                    nestedBegins(
                        new Stack<Tuple<int, dynNode>>(
                            Enumerable.Range(0, InPortData.Count).Select(x => Inputs[x])),
                    preBuilt);
                preBuilt[this] = result;
            }
            return result[outPort];
        }
    }

    //TODO: Setup proper IsDirty smart execution management
    [NodeName("Apply")]
    [NodeCategory(BuiltinNodeCategories.MISC)]
    [NodeDescription("Applies arguments to a function")]
    public class dynApply1 : dynVariableInput
    {
        public dynApply1()
        {
            InPortData.Add(new PortData("func", "Procedure", typeof(object)));
            OutPortData.Add(new PortData("result", "Result", typeof(object)));

            NodeUI.RegisterAllPorts();
        }

        protected override string getInputRootName()
        {
            return "arg";
        }

        protected internal override INode Build(Dictionary<dynNode, Dictionary<int, INode>> preBuilt, int outPort)
        {
            if (!Enumerable.Range(0, InPortData.Count).All(HasInput))
            {
                NodeUI.Error("All inputs must be connected.");
                throw new Exception("Apply Node requires all inputs to be connected.");
            }
            return base.Build(preBuilt, outPort);
        }

        protected override InputNode Compile(IEnumerable<string> portNames)
        {
            return new ApplierNode(portNames);
        }

        protected internal override void RemoveInput()
        {
            if (InPortData.Count > 1)
                base.RemoveInput();
        }

        public override void SaveElement(XmlDocument xmlDoc, XmlElement dynEl)
        {
            //Debug.WriteLine(pd.Object.GetType().ToString());
            foreach (var inport in InPortData.Skip(1))
            {
                XmlElement input = xmlDoc.CreateElement("Input");

                input.SetAttribute("name", inport.NickName);

                dynEl.AppendChild(input);
            }
        }

        public override void LoadElement(XmlNode elNode)
        {
            foreach (XmlNode subNode in elNode.ChildNodes)
            {
                if (subNode.Name == "Input")
                {
                    var attr = subNode.Attributes["name"].Value;

                    if (!attr.Equals("func"))
                        InPortData.Add(new PortData(subNode.Attributes["name"].Value, "", typeof(object)));
                }
            }
            NodeUI.RegisterAllPorts();
        }
    }

    //TODO: Setup proper IsDirty smart execution management
    [NodeName("If")]
    [NodeCategory(BuiltinNodeCategories.BOOLEAN)]
    [NodeDescription("Conditional statement")]
    public class dynConditional : dynNode
    {
        public dynConditional()
        {
            InPortData.Add(new PortData("test", "Test block", typeof(bool)));
            InPortData.Add(new PortData("true", "True block", typeof(object)));
            InPortData.Add(new PortData("false", "False block", typeof(object)));
            OutPortData.Add(new PortData("result", "Result", typeof(object)));

            NodeUI.nickNameBlock.FontSize = 20;

            NodeUI.RegisterAllPorts();
        }

        protected internal override INode Build(Dictionary<dynNode, Dictionary<int, INode>> preBuilt, int outPort)
        {
            if (!Enumerable.Range(0, InPortData.Count).All(HasInput))
            {
                NodeUI.Error("All inputs must be connected.");
                throw new Exception("If Node requires all inputs to be connected.");
            }
            return base.Build(preBuilt, outPort);
        }

        protected override InputNode Compile(IEnumerable<string> portNames)
        {
            return new ConditionalNode(portNames);
        }
    }
    
    [NodeName("Debug Breakpoint")]
    [NodeCategory(BuiltinNodeCategories.DEBUG)]
    [NodeDescription("Halts execution until user clicks button.")]
    public class dynBreakpoint : dynNodeWithOneOutput
    {
        System.Windows.Controls.Button button;

        public dynBreakpoint()
        {
            //add a text box to the input grid of the control
            button = new System.Windows.Controls.Button();
            button.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            button.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            //inputGrid.RowDefinitions.Add(new RowDefinition());
            NodeUI.inputGrid.Children.Add(button);
            System.Windows.Controls.Grid.SetColumn(button, 0);
            System.Windows.Controls.Grid.SetRow(button, 0);
            button.Content = "Continue";

            enabled = false;

            button.Click += new RoutedEventHandler(button_Click);

            InPortData.Add(new PortData("", "Object to inspect", typeof(object)));
            OutPortData.Add(new PortData("", "Object inspected", typeof(object)));

            NodeUI.RegisterAllPorts();
        }

        private bool _enabled;
        private bool enabled
        {
            get { return _enabled; }
            set
            {
                _enabled = value;
                button.IsEnabled = value;
            }
        }

        void button_Click(object sender, RoutedEventArgs e)
        {
            NodeUI.Deselect();
            enabled = false;
        }

        public override Value Evaluate(FSharpList<Value> args)
        {
            var result = args[0];

            Bench.Dispatcher.Invoke(new Action(
               delegate
               {
                   Bench.Log(FScheme.print(result));
               }
            ));

            if (Controller.RunInDebug)
            {
                button.Dispatcher.Invoke(new Action(
                   delegate
                   {
                       enabled = true;
                       NodeUI.Select();
                       Controller.ShowElement(this);
                   }
                ));

                while (enabled)
                {
                    Thread.Sleep(1);
                }
            }

            return result;
        }
    }

    #endregion

    #region Interactive Primitive Types

    #region Base Classes

    class dynTextBox : TextBox
    {
        public event Action OnChangeCommitted;

        private static Brush clear = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0, 0, 0, 0));
        //private static Brush waiting = Brushes.Orange;

        public dynTextBox()
        {
            //turn off the border
            Background = clear;
            BorderThickness = new Thickness(0);
        }

        private bool numeric;
        public bool IsNumeric
        {
            get { return numeric; }
            set
            {
                numeric = value;
                if (value && Text.Length > 0)
                {
                    Text = DynamoController.RemoveChars(
                       Text,
                       Text.ToCharArray()
                          .Where(c => !char.IsDigit(c) && c != '-' && c != '.')
                          .Select(c => c.ToString())
                    );
                }
            }
        }

        private bool pending;
        public bool Pending
        {
            get { return pending; }
            set
            {
                if (value)
                {
                    FontStyle = FontStyles.Italic;
                    FontWeight = FontWeights.Bold;
                }
                else
                {
                    FontStyle = FontStyles.Normal;
                    FontWeight = FontWeights.Normal;
                }
                pending = value;
            }
        }

        private void commit()
        {
            if (OnChangeCommitted != null)
            {
                OnChangeCommitted();
            }
            Pending = false;
        }

        new public string Text
        {
            get { return base.Text; }
            set
            {
                base.Text = value;
                commit();
            }
        }

        private bool shouldCommit()
        {
            return !dynSettings.Controller.DynamicRunEnabled;
        }

        protected override void OnTextChanged(TextChangedEventArgs e)
        {
            Pending = true;

            if (IsNumeric)
            {
                var p = CaretIndex;

                base.Text = DynamoController.RemoveChars(
                   Text,
                   Text.ToCharArray()
                      .Where(c => !char.IsDigit(c) && c != '-' && c != '.')
                      .Select(c => c.ToString())
                );

                CaretIndex = p;
            }
        }

        protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Return || e.Key == System.Windows.Input.Key.Enter)
            {
                commit();
            }
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            commit();
        }
    }

    [IsInteractive(true)]
    public abstract class dynBasicInteractive<T> : dynNodeWithOneOutput
    {
        private T _value = default(T);
        public virtual T Value
        {
            get
            {
                return _value;
            }
            set
            {
                if (_value == null || !_value.Equals(value))
                {
                    _value = value;
                    RequiresRecalc = value != null;
                }
            }
        }

        protected abstract T DeserializeValue(string val);

        public dynBasicInteractive()
        {
            Type type = typeof(T);
            OutPortData.Add(new PortData("", type.Name, type));
            
            //add an edit window option to the 
            //main context window
            System.Windows.Controls.MenuItem editWindowItem = new System.Windows.Controls.MenuItem();
            editWindowItem.Header = "Edit...";
            editWindowItem.IsCheckable = false;

            NodeUI.MainContextMenu.Items.Add(editWindowItem);

            editWindowItem.Click += new RoutedEventHandler(editWindowItem_Click);
        }

        public virtual void editWindowItem_Click(object sender, RoutedEventArgs e)
        {
            //override in child classes
        }

        public override void SaveElement(XmlDocument xmlDoc, XmlElement dynEl)
        {
            //Debug.WriteLine(pd.Object.GetType().ToString());
            XmlElement outEl = xmlDoc.CreateElement(typeof(T).FullName);
            outEl.SetAttribute("value", Value.ToString());
            dynEl.AppendChild(outEl);
        }

        public override void LoadElement(XmlNode elNode)
        {
            foreach (XmlNode subNode in elNode.ChildNodes)
            {
                if (subNode.Name.Equals(typeof(T).FullName))
                {
                    Value = DeserializeValue(subNode.Attributes[0].Value);
                }
            }
        }

        public override string PrintExpression()
        {
            return Value.ToString();
        }
    }

    public abstract class dynDouble : dynBasicInteractive<double>
    {
        public override Value Evaluate(FSharpList<Value> args)
        {
            return FScheme.Value.NewNumber(Value);
        }

        public override void editWindowItem_Click(object sender, RoutedEventArgs e)
        {

            dynEditWindow editWindow = new dynEditWindow();

            //set the text of the edit window to begin
            editWindow.editText.Text = base.Value.ToString();

            if (editWindow.ShowDialog() != true)
            {
                return;
            }

            //set the value from the text in the box
            Value = DeserializeValue(editWindow.editText.Text);
        }
    }

    public abstract class dynBool : dynBasicInteractive<bool>
    {
        public override Value Evaluate(FSharpList<Value> args)
        {
            return FScheme.Value.NewNumber(Value ? 1 : 0);
        }
    }

    public abstract class dynString : dynBasicInteractive<string>
    {
        public override string Value
        {
            get
            {
                return base.Value;
            }
            set
            {
                base.Value = EscapeString(value);
            }
        }

        // Taken from:
        // http://stackoverflow.com/questions/6378681/how-can-i-use-net-style-escape-sequences-in-runtime-values
        private static string EscapeString(string s)
        {
            Contract.Requires(s != null);
            Contract.Ensures(Contract.Result<string>() != null);

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\')
                {
                    i++;
                    if (i == s.Length)
                        throw new ArgumentException("Escape sequence starting at end of string", s);
                    switch (s[i])
                    {
                        case '\\':
                            sb.Append('\\');
                            break;
                        case 't':
                            sb.Append('\t');
                            break;
                        case 'n':
                            sb.Append('\n');
                            break;
                        case 'r':
                            sb.Append('\r');
                            break;
                        //TODO: ADD MORE CASES HERE
                    }
                }
                else
                    sb.Append(s[i]);
            }
            return sb.ToString();
        }

        public override Value Evaluate(FSharpList<Value> args)
        {
            return FScheme.Value.NewString(Value);
        }

        public override string PrintExpression()
        {
            return "\"" + base.PrintExpression() + "\"";
        }

        public override void editWindowItem_Click(object sender, RoutedEventArgs e)
        {

            dynEditWindow editWindow = new dynEditWindow();

            //set the text of the edit window to begin
            editWindow.editText.Text = base.Value.ToString();

            if (editWindow.ShowDialog() != true)
            {
                return;
            }

            //set the value from the text in the box
            Value = DeserializeValue(editWindow.editText.Text);
        }
    }

    #endregion

    [NodeName("Number")]
    [NodeCategory(BuiltinNodeCategories.PRIMITIVES)]
    [NodeDescription("Creates a number.")]
    public class dynDoubleInput : dynDouble
    {
        dynTextBox tb;
        //TextBlock nodeLabel;

        public dynDoubleInput()
        {
            //add a text box to the input grid of the control
            tb = new dynTextBox();
            tb.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            tb.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            NodeUI.inputGrid.Children.Add(tb);
            System.Windows.Controls.Grid.SetColumn(tb, 0);
            System.Windows.Controls.Grid.SetRow(tb, 0);
            tb.IsNumeric = true;
            tb.Text = "0.0";
            tb.OnChangeCommitted += delegate { Value = DeserializeValue(tb.Text); };

            NodeUI.RegisterAllPorts();

            //take out the left and right margins
            //and make this so it's not so wide
            NodeUI.inputGrid.Margin = new Thickness(10, 5, 10, 5);
            NodeUI.topControl.Width = 100;
            NodeUI.topControl.Height = 50;

            NodeUI.UpdateLayout();
        }

        public override double Value
        {
            get
            {
                return base.Value;
            }
            set
            {
                if (base.Value == value)
                    return;

                base.Value = value;

                //nodeLabel.Text = dynUtils.Ellipsis(value.ToString(), 5);
                tb.Text = value.ToString();
                tb.Pending = false;
            }
        }

        protected override double DeserializeValue(string val)
        {
            try
            {
                return Convert.ToDouble(val);
            }
            catch
            {
                return 0;
            }
        }


    }

    //MDJ - added by Matt Jezyk 10.27.2011
    [NodeName("Number Slider")]
    [NodeCategory(BuiltinNodeCategories.PRIMITIVES)]
    [NodeDescription("Creates a number, but using SLIDERS!.")]
    public class dynDoubleSliderInput : dynDouble
    {
        Slider tb_slider;
        dynTextBox mintb;
        dynTextBox maxtb;
        TextBox displayBox;

        public dynDoubleSliderInput()
        {
            NodeUI.topControl.Width = 200;

            //add a slider control to the input grid of the control
            tb_slider = new System.Windows.Controls.Slider();
            tb_slider.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            tb_slider.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            NodeUI.inputGrid.Children.Add(tb_slider);
            System.Windows.Controls.Grid.SetColumn(tb_slider, 1);
            System.Windows.Controls.Grid.SetRow(tb_slider, 0);
            tb_slider.Value = 0.0;
            tb_slider.Maximum = 100.0;
            tb_slider.Minimum = 0.0;
            tb_slider.Ticks = new System.Windows.Media.DoubleCollection(10);
            tb_slider.TickPlacement = System.Windows.Controls.Primitives.TickPlacement.BottomRight;
            tb_slider.ValueChanged += delegate
            {
                Value = tb_slider.Value;

                var pos = Mouse.GetPosition(NodeUI.elementCanvas);
                Canvas.SetLeft(displayBox, pos.X);
            };

            tb_slider.PreviewMouseDown += delegate
            {
                if (NodeUI.IsEnabled && !NodeUI.elementCanvas.Children.Contains(displayBox))
                {
                    NodeUI.elementCanvas.Children.Add(displayBox);

                    var pos = Mouse.GetPosition(NodeUI.elementCanvas);
                    Canvas.SetLeft(displayBox, pos.X);
                }
            };

            tb_slider.PreviewMouseUp += delegate
            {
                if (NodeUI.elementCanvas.Children.Contains(displayBox))
                    NodeUI.elementCanvas.Children.Remove(displayBox);
            };

            mintb = new dynTextBox();
            //mintb.MaxLength = 3;
            mintb.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            mintb.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            mintb.Width = double.NaN;
            mintb.IsNumeric = true;
            mintb.Text = "0";
            mintb.OnChangeCommitted += delegate
            {
                try
                {
                    tb_slider.Minimum = Convert.ToDouble(mintb.Text);
                }
                catch
                {
                    tb_slider.Minimum = 0;
                }
            };
            //mintb.Pending = false;

            maxtb = new dynTextBox();
            //maxtb.MaxLength = 3;
            maxtb.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            maxtb.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            maxtb.Width = double.NaN;
            maxtb.IsNumeric = true;
            maxtb.Text = "100";
            maxtb.OnChangeCommitted += delegate
            {
                try
                {
                    tb_slider.Maximum = Convert.ToDouble(maxtb.Text);
                }
                catch
                {
                    tb_slider.Maximum = 0;
                }
            };
            //maxtb.Pending = false;

            NodeUI.SetColumnAmount(3);
            NodeUI.inputGrid.Children.Add(mintb);
            NodeUI.inputGrid.Children.Add(maxtb);

            //make the middle column containing the slider
            //take up most of the width
            NodeUI.inputGrid.ColumnDefinitions[1].Width = new GridLength(.60 * NodeUI.Width);

            System.Windows.Controls.Grid.SetColumn(mintb, 0);
            System.Windows.Controls.Grid.SetColumn(maxtb, 2);

            NodeUI.RegisterAllPorts();

            NodeUI.inputGrid.Margin = new Thickness(10, 5, 10, 5);

            displayBox = new TextBox()
            {
                IsReadOnly = true,
                Background = Brushes.White,
                Foreground = Brushes.Black
            };
            Canvas.SetTop(displayBox, NodeUI.Height);
            Canvas.SetZIndex(displayBox, int.MaxValue);

            var binding = new System.Windows.Data.Binding("Value")
            {
                Source = tb_slider,
                Mode = System.Windows.Data.BindingMode.OneWay,
                Converter = new DoubleDisplay()
            };
            displayBox.SetBinding(TextBox.TextProperty, binding);
        }

        #region Value Conversion
        [ValueConversion(typeof(double), typeof(String))]
        private class DoubleDisplay : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return ((double)value).ToString("F4");
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return null;
            }
        }
        #endregion

        protected override double DeserializeValue(string val)
        {
            try
            {
                return Convert.ToDouble(val);
            }
            catch
            {
                return 0;
            }
        }

        public override double Value
        {
            set
            {
                if (base.Value == value)
                    return;

                if (value > tb_slider.Maximum)
                {
                    maxtb.Text = value.ToString();
                    tb_slider.Maximum = value;
                    //maxtb.Pending = false;
                }
                if (value < tb_slider.Minimum)
                {
                    mintb.Text = value.ToString();
                    tb_slider.Minimum = value;
                    //mintb.Pending = false;
                }

                base.Value = value;
                tb_slider.Value = value;
            }
        }

        public override void SaveElement(XmlDocument xmlDoc, XmlElement dynEl)
        {
            XmlElement outEl = xmlDoc.CreateElement(typeof(double).FullName);
            outEl.SetAttribute("value", Value.ToString());
            outEl.SetAttribute("min", tb_slider.Minimum.ToString());
            outEl.SetAttribute("max", tb_slider.Maximum.ToString());
            dynEl.AppendChild(outEl);
        }

        public override void LoadElement(XmlNode elNode)
        {
            foreach (XmlNode subNode in elNode.ChildNodes)
            {
                if (subNode.Name.Equals(typeof(double).FullName))
                {
                    foreach (XmlAttribute attr in subNode.Attributes)
                    {
                        if (attr.Name.Equals("value"))
                            Value = DeserializeValue(attr.Value);
                        else if (attr.Name.Equals("min"))
                        {
                            //tb_slider.Minimum = Convert.ToDouble(attr.Value);
                            mintb.Text = attr.Value;
                        }
                        else if (attr.Name.Equals("max"))
                        {
                            //tb_slider.Maximum = Convert.ToDouble(attr.Value);
                            maxtb.Text = attr.Value;
                        }
                    }
                }
            }
        }

    }

    [NodeName("Boolean")]
    [NodeCategory(BuiltinNodeCategories.PRIMITIVES)]
    [NodeDescription("Selection between a true and false.")]
    [NodeSearchTags("true", "truth", "false")]
    public class dynBoolSelector : dynBool
    {
        System.Windows.Controls.RadioButton rbTrue;
        System.Windows.Controls.RadioButton rbFalse;

        public dynBoolSelector()
        {
            //inputGrid.Margin = new System.Windows.Thickness(5,5,20,5);

            //add a text box to the input grid of the control
            rbTrue = new System.Windows.Controls.RadioButton();
            rbFalse = new System.Windows.Controls.RadioButton();
            rbTrue.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            rbFalse.VerticalAlignment = System.Windows.VerticalAlignment.Center;

            //use a unique name for the button group
            //so other instances of this element don't get confused
            string groupName = Guid.NewGuid().ToString();
            rbTrue.GroupName = groupName;
            rbFalse.GroupName = groupName;

            rbTrue.Content = "1";
            rbFalse.Content = "0";

            RowDefinition rd = new RowDefinition();
            ColumnDefinition cd1 = new ColumnDefinition();
            ColumnDefinition cd2 = new ColumnDefinition();
            NodeUI.inputGrid.ColumnDefinitions.Add(cd1);
            NodeUI.inputGrid.ColumnDefinitions.Add(cd2);
            NodeUI.inputGrid.RowDefinitions.Add(rd);

            NodeUI.inputGrid.Children.Add(rbTrue);
            NodeUI.inputGrid.Children.Add(rbFalse);

            System.Windows.Controls.Grid.SetColumn(rbTrue, 0);
            System.Windows.Controls.Grid.SetRow(rbTrue, 0);
            System.Windows.Controls.Grid.SetColumn(rbFalse, 1);
            System.Windows.Controls.Grid.SetRow(rbFalse, 0);

            rbFalse.IsChecked = true;
            rbTrue.Checked += new System.Windows.RoutedEventHandler(rbTrue_Checked);
            rbFalse.Checked += new System.Windows.RoutedEventHandler(rbFalse_Checked);
            //outPort.Object = false;

            NodeUI.RegisterAllPorts();
        }

        protected override bool DeserializeValue(string val)
        {
            try
            {
                return val.ToLower().Equals("true");
            }
            catch
            {
                return false;
            }
        }

        public override bool Value
        {
            set
            {
                base.Value = value;
                if (value)
                {
                    rbFalse.IsChecked = false;
                    rbTrue.IsChecked = true;
                }
                else
                {
                    rbFalse.IsChecked = true;
                    rbTrue.IsChecked = false;
                }
            }
        }

        void rbFalse_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            Value = false;
        }

        void rbTrue_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            Value = true;
        }
    }

    [NodeName("String")]
    [NodeCategory(BuiltinNodeCategories.PRIMITIVES)]
    [NodeDescription("Creates a string.")]
    public class dynStringInput : dynString
    {
        dynTextBox tb;
        //TextBlock tb;

        public dynStringInput()
        {
            //add a text box to the input grid of the control
            tb = new dynTextBox();
            //tb = new TextBlock();
            tb.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            tb.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            NodeUI.inputGrid.Children.Add(tb);
            System.Windows.Controls.Grid.SetColumn(tb, 0);
            System.Windows.Controls.Grid.SetRow(tb, 0);
            tb.Text = "";

            tb.OnChangeCommitted += delegate { Value = tb.Text; };

            NodeUI.RegisterAllPorts();

            //remove the margins
            NodeUI.inputGrid.Margin = new Thickness(10, 5, 10, 5);
        }

        public override string Value
        {
            set
            {
                if (base.Value == value)
                    return;

                base.Value = value;

                tb.Text = Utilities.Ellipsis(Value, 30);
            }
        }

        /*
        void tb_LostFocus(object sender, RoutedEventArgs e)
        {
            Value = tb.Text;
        }

        void tb_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key.Equals(Keys.Enter))
                Value = tb.Text;
        }*/

        protected override string DeserializeValue(string val)
        {
            return val;
        }

        public override void SaveElement(XmlDocument xmlDoc, XmlElement dynEl)
        {
            XmlElement outEl = xmlDoc.CreateElement(typeof(string).FullName);
            outEl.SetAttribute("value", System.Web.HttpUtility.UrlEncode(Value.ToString()));
            dynEl.AppendChild(outEl);
        }

        public override void LoadElement(XmlNode elNode)
        {
            foreach (XmlNode subNode in elNode.ChildNodes)
            {
                if (subNode.Name.Equals(typeof(string).FullName))
                {
                    foreach (XmlAttribute attr in subNode.Attributes)
                    {
                        if (attr.Name.Equals("value"))
                        {
                            Value = DeserializeValue(System.Web.HttpUtility.UrlDecode(attr.Value));
                            tb.Text = Utilities.Ellipsis(Value, 30);
                        }

                    }
                }
            }
        }
    }

    [NodeName("Filename")]
    [NodeCategory(BuiltinNodeCategories.PRIMITIVES)]
    [NodeDescription("Allows you to select a file on the system to get its filename.")]
    public class dynStringFilename : dynBasicInteractive<string>
    {
        System.Windows.Controls.TextBox tb;

        public dynStringFilename()
        {
            //add a button to the inputGrid on the dynElement
            System.Windows.Controls.Button readFileButton = new System.Windows.Controls.Button();
            readFileButton.Margin = new System.Windows.Thickness(0, 0, 0, 0);
            readFileButton.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            readFileButton.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            readFileButton.Click += new System.Windows.RoutedEventHandler(readFileButton_Click);
            readFileButton.Content = "Browse...";
            readFileButton.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            readFileButton.VerticalAlignment = System.Windows.VerticalAlignment.Center;

            tb = new TextBox();
            tb.Text = "No file selected.";
            tb.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            tb.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            SolidColorBrush backgroundBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0, 0, 0, 0));
            tb.Background = backgroundBrush;
            tb.BorderThickness = new Thickness(0);
            tb.IsReadOnly = true;
            tb.IsReadOnlyCaretVisible = false;
            tb.TextChanged += delegate { tb.ScrollToHorizontalOffset(double.PositiveInfinity); };

            NodeUI.SetRowAmount(2);

            NodeUI.inputGrid.Children.Add(tb);
            NodeUI.inputGrid.Children.Add(readFileButton);

            System.Windows.Controls.Grid.SetRow(readFileButton, 0);
            System.Windows.Controls.Grid.SetRow(tb, 1);

            NodeUI.RegisterAllPorts();

            NodeUI.topControl.Height = 60;
            NodeUI.UpdateLayout();
        }

        public override string Value
        {
            get
            {
                return base.Value;
            }
            set
            {
                base.Value = value;

                tb.Text = string.IsNullOrEmpty(Value)
                   ? "No file selected."
                   : Value;
            }
        }

        protected override string DeserializeValue(string val)
        {
            if (File.Exists(val))
            {
                return val;
            }
            else
            {
                return "";
            }
        }

        void readFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openDialog = new OpenFileDialog();

            if (openDialog.ShowDialog() == DialogResult.OK)
            {
                Value = openDialog.FileName;
            }
        }

        public override Value Evaluate(FSharpList<Value> args)
        {
            if (string.IsNullOrEmpty(Value))
                throw new Exception("No file selected.");

            return FScheme.Value.NewString(Value);
        }

        public override string PrintExpression()
        {
            return "\"" + base.PrintExpression() + "\"";
        }
    }

    #endregion

    #region Strings and Conversions

    [NodeName("Concatenate Strings")]
    [NodeDescription("Concatenates two or more strings")]
    [NodeCategory(BuiltinNodeCategories.STRINGS)]
    public class dynConcatStrings : dynVariableInput
    {
        public dynConcatStrings()
        {
            InPortData.Add(new PortData("s1", "First string", typeof(string)));
            InPortData.Add(new PortData("s2", "Second string", typeof(string)));
            OutPortData.Add(new PortData("combined", "Combined lists", typeof(string)));

            NodeUI.RegisterAllPorts();
        }

        protected override string getInputRootName()
        {
            return "s";
        }

        protected override int getNewInputIndex()
        {
            return InPortData.Count + 1;
        }

        protected internal override void RemoveInput()
        {
            if (InPortData.Count > 2)
                base.RemoveInput();
        }

        public override void SaveElement(XmlDocument xmlDoc, XmlElement dynEl)
        {
            //Debug.WriteLine(pd.Object.GetType().ToString());
            foreach (var inport in InPortData.Skip(2))
            {
                XmlElement input = xmlDoc.CreateElement("Input");

                input.SetAttribute("name", inport.NickName);

                dynEl.AppendChild(input);
            }
        }

        public override void LoadElement(XmlNode elNode)
        {
            foreach (XmlNode subNode in elNode.ChildNodes)
            {
                if (subNode.Name == "Input")
                {
                    var attr = subNode.Attributes["name"].Value;

                    InPortData.Add(new PortData(subNode.Attributes["name"].Value, "", typeof(object)));
                }
            }
            NodeUI.RegisterAllPorts();
        }

        protected override InputNode Compile(IEnumerable<string> portNames)
        {
            if (SaveResult)
                return base.Compile(portNames);
            else
                return new FunctionNode("concat-strings", portNames);
        }

        public override Value Evaluate(FSharpList<Value> args)
        {
            return ((Value.Function)Controller.FSchemeEnvironment.LookupSymbol("concat-strings"))
                .Item.Invoke(args);
        }
    }

    [NodeName("String -> Number")]
    [NodeDescription("Converts a string to a number")]
    [NodeCategory(BuiltinNodeCategories.STRINGS)]
    public class dynString2Num : dynBuiltinFunction
    {
        public dynString2Num()
            : base("string->num")
        {
            InPortData.Add(new PortData("s", "A string", typeof(string)));
            OutPortData.Add(new PortData("n", "A number", typeof(double)));

            NodeUI.RegisterAllPorts();
        }
    }

    [NodeName("Number -> String")]
    [NodeDescription("Converts a number to a string")]
    [NodeCategory(BuiltinNodeCategories.STRINGS)]
    public class dynNum2String : dynBuiltinFunction
    {
        public dynNum2String()
            : base("num->string")
        {
            InPortData.Add(new PortData("n", "A number", typeof(double)));
            OutPortData.Add(new PortData("s", "A string", typeof(string)));
            NodeUI.RegisterAllPorts();
        }
    }

    [NodeName("Split String")]
    [NodeDescription("Splits given string around given delimiter into a list of sub strings.")]
    [NodeCategory(BuiltinNodeCategories.STRINGS)]
    public class dynSplitString : dynNodeWithOneOutput
    {
        public dynSplitString()
        {
            InPortData.Add(new PortData("str", "String to split", typeof(string)));
            InPortData.Add(new PortData("del", "Delimiter", typeof(string)));
            OutPortData.Add(new PortData("strs", "List of split strings", typeof(IList<string>)));

            NodeUI.RegisterAllPorts();
        }

        public override Value Evaluate(FSharpList<Value> args)
        {
            string str = ((Value.String)args[0]).Item;
            string del = ((Value.String)args[1]).Item;

            return Value.NewList(
                Utils.SequenceToFSharpList(
                    str.Split(new string[] { del }, StringSplitOptions.None)
                       .Select(Value.NewString)
                )
            );
        }
    }

    [NodeName("Join Strings")]
    [NodeDescription("Joins the given list of strings around the given delimiter.")]
    [NodeCategory(BuiltinNodeCategories.STRINGS)]
    public class dynJoinStrings : dynNodeWithOneOutput
    {
        public dynJoinStrings()
        {
            InPortData.Add(new PortData("strs", "List of strings to join.", typeof(IList<string>)));
            InPortData.Add(new PortData("del", "Delimier", typeof(string)));
            OutPortData.Add(new PortData("str", "Joined string", typeof(string)));

            NodeUI.RegisterAllPorts();
        }

        public override Value Evaluate(FSharpList<Value> args)
        {
            var strs = ((Value.List)args[0]).Item;
            var del = ((Value.String)args[1]).Item;

            return Value.NewString(
                string.Join(del, strs.Select(x => ((Value.String)x).Item))
            );
        }
    }

    [NodeName("String Case")]
    [NodeDescription("Converts a string to uppercase or lowercase")]
    [NodeCategory(BuiltinNodeCategories.STRINGS)]
    public class dynStringCase : dynNodeWithOneOutput
    {
        public dynStringCase()
        {
            InPortData.Add(new PortData("str", "String to convert", typeof(string)));
            InPortData.Add(new PortData("upper?", "True = Uppercase, False = Lowercase", typeof(bool)));
            OutPortData.Add(new PortData("s", "Converted string", typeof(string)));

            NodeUI.RegisterAllPorts();
        }

        public override Value Evaluate(FSharpList<Value> args)
        {
            string s = ((Value.String)args[0]).Item;
            bool upper = ((Value.Number)args[1]).Item == 1.0;

            return Value.NewString(
                upper ? s.ToUpper() : s.ToLower()
            );
        }
    }

    [NodeName("Substring")]
    [NodeDescription("Gets a substring of a given string")]
    [NodeCategory(BuiltinNodeCategories.STRINGS)]
    public class dynSubstring : dynNodeWithOneOutput
    {
        public dynSubstring()
        {
            InPortData.Add(new PortData("str", "String to take substring from", typeof(string)));
            InPortData.Add(new PortData("start", "Starting index of substring", typeof(double)));
            InPortData.Add(new PortData("length", "Length of substring", typeof(double)));
            OutPortData.Add(new PortData("sub", "Substring", typeof(string)));

            NodeUI.RegisterAllPorts();
        }

        public override Value Evaluate(FSharpList<Value> args)
        {
            string s = ((Value.String)args[0]).Item;
            double start = ((Value.Number)args[1]).Item;
            double length = ((Value.Number)args[2]).Item;

            return Value.NewString(s.Substring((int)start, (int)length));
        }
    }

    #endregion
}
