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

using Microsoft.FSharp.Core;
using Microsoft.FSharp.Collections;
using Value = Dynamo.FScheme.Value;
using Expression = Dynamo.FScheme.Expression;

namespace Dynamo.FSchemeInterop
{
    /// <summary>
    /// Miscellaneous helper and convenience methods.
    /// </summary>
    public static class Utils
    {
        /// <summary>
        /// Makes an FScheme Expression representing an anonymous function.
        /// </summary>
        public static Expression MakeAnon(IEnumerable<string> inputSyms, Expression body)
        {
            return Expression.NewFun(
                SequenceToFSharpList(inputSyms.Select(FScheme.Parameter.NewNormal)),
                body);
        }

        /// <summary>
        /// Makes an FScheme Expression representing an anonymous function, where all extra
        /// arguments are packed into the last parameter.
        /// </summary>
        /// <param name="inputSyms">List of parameters</param>
        /// <param name="body">Body of the function</param>
        /// <returns></returns>
        public static Expression MakeVarArgAnon(IEnumerable<string> inputSyms, Expression body)
        {
            var cnt = inputSyms.Count();

            return Expression.NewFun(
                SequenceToFSharpList(inputSyms.Select(
                    (x, i) => 
                        i == cnt 
                        ? FScheme.Parameter.NewTail(x) 
                        : FScheme.Parameter.NewNormal(x))),
                body);
        }

        /// <summary>
        /// Converts a Func to an FSharpFunc.
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        public static FSharpFunc<FSharpList<Value>, Value> ConvertToFSchemeFunc(Converter<FSharpList<Value>, Value> f)
        {
            return FSharpFunc<FSharpList<Value>, Value>.FromConverter(f);
        }

        /// <summary>
        /// Makes an FSharp list from all given arguments.
        /// </summary>
        public static FSharpList<T> MakeFSharpList<T>(params T[] ar)
        {
            FSharpList<T> foo = FSharpList<T>.Empty;
            for (int n = ar.Length - 1; n >= 0; n--)
                foo = FSharpList<T>.Cons(ar[n], foo);
            return foo;
        }

        /// <summary>
        /// Converts the given IEnumerable into an FSharp list.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="seq"></param>
        /// <returns></returns>
        public static FSharpList<T> SequenceToFSharpList<T>(IEnumerable<T> seq)
        {
            FSharpList<T> result = FSharpList<T>.Empty;
            foreach (T element in seq.Reverse<T>())
                result = FSharpList<T>.Cons(element, result);
            return result;
        }

        /// <summary>
        /// A better ToString() for Values.
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public static string Print(this Value v)
        {
            return FScheme.print(v);
        }
    }
}
