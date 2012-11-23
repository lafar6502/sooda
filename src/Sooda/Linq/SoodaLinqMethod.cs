//
// Copyright (c) 2012 Piotr Fusik <piotr@fusik.info>
//
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions
// are met:
//
// * Redistributions of source code must retain the above copyright notice,
//   this list of conditions and the following disclaimer.
//
// * Redistributions in binary form must reproduce the above copyright notice,
//   this list of conditions and the following disclaimer in the documentation
//   and/or other materials provided with the distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
// ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF
// THE POSSIBILITY OF SUCH DAMAGE.
//

#if DOTNET35

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

enum SoodaLinqMethod
{
    Unknown,
    Queryable_Where,
    Queryable_OrderBy,
    Queryable_OrderByDescending,
    Queryable_ThenBy,
    Queryable_ThenByDescending,
    Queryable_Take,
    Queryable_Select,
    Queryable_SelectIndexed,
    Queryable_Reverse,
    Queryable_Except,
    Queryable_Intersect,
    Queryable_Union,
    Queryable_All,
    Queryable_Any,
    Queryable_AnyFiltered,
    Queryable_Count,
    Queryable_CountFiltered,
    Queryable_First,
    Queryable_FirstFiltered,
    Queryable_FirstOrDefault,
    Queryable_FirstOrDefaultFiltered,
    Queryable_Last,
    Queryable_LastFiltered,
    Queryable_LastOrDefault,
    Queryable_LastOrDefaultFiltered,
    Queryable_Single,
    Queryable_SingleFiltered,
    Queryable_SingleOrDefault,
    Queryable_SingleOrDefaultFiltered,
    Queryable_Average,
    Queryable_AverageNullable,
    Queryable_Max,
    Queryable_Min,
    Queryable_SumDecimal,
    Queryable_SumDouble,
    Queryable_SumInt,
    Queryable_SumLong,
    Enumerable_All,
    Enumerable_Any,
    Enumerable_AnyFiltered,
    Enumerable_Contains,
    Enumerable_Count,
    ICollection_Contains,
    Object_GetType,
    String_Concat,
    String_Like,
    String_Remove,
    String_Replace,
    String_ToLower,
    String_ToUpper,
    Math_Abs,
    Math_Acos,
    Math_Asin,
    Math_Atan,
    Math_Cos,
    Math_Exp,
    Math_Floor,
    Math_Pow,
    Math_Round,
    Math_Sign,
    Math_Sin,
    Math_Sqrt,
    Math_Tan,
}

static class SoodaLinqMethodUtil
{
    static Dictionary<MethodInfo, SoodaLinqMethod> _dict;

    static MethodInfo Ungeneric(MethodInfo method)
    {
        return method.IsGenericMethod ? method.GetGenericMethodDefinition() : method;
    }

    static MethodInfo MethodOf(Expression<Action> lambda)
    {
        return Ungeneric(((MethodCallExpression) lambda.Body).Method);
    }

    public static SoodaLinqMethod Get(MethodInfo method)
    {
        Dictionary<MethodInfo, SoodaLinqMethod> dict = _dict;
        if (dict == null)
        {
            dict = new Dictionary<MethodInfo, SoodaLinqMethod>();
            Expression<Func<object, bool>> predicate = o => true;
            Expression<Func<object, int>> selector = o => 0;
            Expression<Func<object, decimal>> selectorM = o => 0;
            Expression<Func<object, double>> selectorD = o => 0;
            Expression<Func<object, long>> selectorL = o => 0;
            Expression<Func<object, int?>> selectorN = o => 0;
            Expression<Func<object, decimal?>> selectorNM = o => 0;
            Expression<Func<object, double?>> selectorND = o => 0;
            Expression<Func<object, long?>> selectorNL = o => 0;
            dict.Add(MethodOf(() => Queryable.Where(null, predicate)), SoodaLinqMethod.Queryable_Where);
            dict.Add(MethodOf(() => Queryable.OrderBy(null, selector)), SoodaLinqMethod.Queryable_OrderBy);
            dict.Add(MethodOf(() => Queryable.OrderByDescending(null, selector)), SoodaLinqMethod.Queryable_OrderByDescending);
            dict.Add(MethodOf(() => Queryable.ThenBy(null, selector)), SoodaLinqMethod.Queryable_ThenBy);
            dict.Add(MethodOf(() => Queryable.ThenByDescending(null, selector)), SoodaLinqMethod.Queryable_ThenByDescending);
            dict.Add(MethodOf(() => Queryable.Take<object>(null, 0)), SoodaLinqMethod.Queryable_Take);
            dict.Add(MethodOf(() => Queryable.Select(null, selector)), SoodaLinqMethod.Queryable_Select);
            dict.Add(MethodOf(() => Queryable.Select(null, (object o, int i) => i)), SoodaLinqMethod.Queryable_SelectIndexed);
            dict.Add(MethodOf(() => Queryable.Reverse<object>(null)), SoodaLinqMethod.Queryable_Reverse);
            dict.Add(MethodOf(() => Queryable.Except<object>(null, null)), SoodaLinqMethod.Queryable_Except);
            dict.Add(MethodOf(() => Queryable.Intersect<object>(null, null)), SoodaLinqMethod.Queryable_Intersect);
            dict.Add(MethodOf(() => Queryable.Union<object>(null, null)), SoodaLinqMethod.Queryable_Union);
            dict.Add(MethodOf(() => Queryable.All(null, predicate)), SoodaLinqMethod.Queryable_All);
            dict.Add(MethodOf(() => Queryable.Any<object>(null)), SoodaLinqMethod.Queryable_Any);
            dict.Add(MethodOf(() => Queryable.Any(null, predicate)), SoodaLinqMethod.Queryable_AnyFiltered);
            dict.Add(MethodOf(() => Queryable.Count<object>(null)), SoodaLinqMethod.Queryable_Count);
            dict.Add(MethodOf(() => Queryable.Count(null, predicate)), SoodaLinqMethod.Queryable_CountFiltered);
            dict.Add(MethodOf(() => Queryable.First<object>(null)), SoodaLinqMethod.Queryable_First);
            dict.Add(MethodOf(() => Queryable.First(null, predicate)), SoodaLinqMethod.Queryable_FirstFiltered);
            dict.Add(MethodOf(() => Queryable.FirstOrDefault<object>(null)), SoodaLinqMethod.Queryable_FirstOrDefault);
            dict.Add(MethodOf(() => Queryable.FirstOrDefault(null, predicate)), SoodaLinqMethod.Queryable_FirstOrDefaultFiltered);
            dict.Add(MethodOf(() => Queryable.Last<object>(null)), SoodaLinqMethod.Queryable_Last);
            dict.Add(MethodOf(() => Queryable.Last(null, predicate)), SoodaLinqMethod.Queryable_LastFiltered);
            dict.Add(MethodOf(() => Queryable.LastOrDefault<object>(null)), SoodaLinqMethod.Queryable_LastOrDefault);
            dict.Add(MethodOf(() => Queryable.LastOrDefault(null, predicate)), SoodaLinqMethod.Queryable_LastOrDefaultFiltered);
            dict.Add(MethodOf(() => Queryable.Single<object>(null)), SoodaLinqMethod.Queryable_Single);
            dict.Add(MethodOf(() => Queryable.Single(null, predicate)), SoodaLinqMethod.Queryable_SingleFiltered);
            dict.Add(MethodOf(() => Queryable.SingleOrDefault<object>(null)), SoodaLinqMethod.Queryable_SingleOrDefault);
            dict.Add(MethodOf(() => Queryable.SingleOrDefault(null, predicate)), SoodaLinqMethod.Queryable_SingleOrDefaultFiltered);
            dict.Add(MethodOf(() => Queryable.Average(null, selectorM)), SoodaLinqMethod.Queryable_Average);
            dict.Add(MethodOf(() => Queryable.Average(null, selectorD)), SoodaLinqMethod.Queryable_Average);
            dict.Add(MethodOf(() => Queryable.Average(null, selector)), SoodaLinqMethod.Queryable_Average);
            dict.Add(MethodOf(() => Queryable.Average(null, selectorL)), SoodaLinqMethod.Queryable_Average);
            dict.Add(MethodOf(() => Queryable.Average(null, selectorNM)), SoodaLinqMethod.Queryable_AverageNullable);
            dict.Add(MethodOf(() => Queryable.Average(null, selectorND)), SoodaLinqMethod.Queryable_AverageNullable);
            dict.Add(MethodOf(() => Queryable.Average(null, selectorN)), SoodaLinqMethod.Queryable_AverageNullable);
            dict.Add(MethodOf(() => Queryable.Average(null, selectorNL)), SoodaLinqMethod.Queryable_AverageNullable);
            dict.Add(MethodOf(() => Queryable.Max(null, selector)), SoodaLinqMethod.Queryable_Max);
            dict.Add(MethodOf(() => Queryable.Min(null, selector)), SoodaLinqMethod.Queryable_Min);
            dict.Add(MethodOf(() => Queryable.Sum(null, selectorM)), SoodaLinqMethod.Queryable_SumDecimal);
            dict.Add(MethodOf(() => Queryable.Sum(null, selectorD)), SoodaLinqMethod.Queryable_SumDouble);
            dict.Add(MethodOf(() => Queryable.Sum(null, selector)), SoodaLinqMethod.Queryable_SumInt);
            dict.Add(MethodOf(() => Queryable.Sum(null, selectorL)), SoodaLinqMethod.Queryable_SumLong);
            dict.Add(MethodOf(() => Queryable.Sum(null, selectorNM)), SoodaLinqMethod.Queryable_SumDecimal);
            dict.Add(MethodOf(() => Queryable.Sum(null, selectorND)), SoodaLinqMethod.Queryable_SumDouble);
            dict.Add(MethodOf(() => Queryable.Sum(null, selectorN)), SoodaLinqMethod.Queryable_SumInt);
            dict.Add(MethodOf(() => Queryable.Sum(null, selectorNL)), SoodaLinqMethod.Queryable_SumLong);
            dict.Add(MethodOf(() => Enumerable.All(null, (object o) => true)), SoodaLinqMethod.Enumerable_All);
            dict.Add(MethodOf(() => Enumerable.Any<object>(null)), SoodaLinqMethod.Enumerable_Any);
            dict.Add(MethodOf(() => Enumerable.Any(null, (object o) => true)), SoodaLinqMethod.Enumerable_AnyFiltered);
            dict.Add(MethodOf(() => Enumerable.Contains<object>(null, null)), SoodaLinqMethod.Enumerable_Contains);
            dict.Add(MethodOf(() => Enumerable.Count<object>(null)), SoodaLinqMethod.Enumerable_Count);
            dict.Add(MethodOf(() => ((ICollection<object>) null).Contains(null)), SoodaLinqMethod.ICollection_Contains); // FIXME: Ungeneric doesn't handle methods in generic classes, so this will only work on ICollection<object>
            dict.Add(MethodOf(() => ((System.Collections.IList) null).Contains(null)), SoodaLinqMethod.ICollection_Contains);
            dict.Add(MethodOf(() => string.Empty.GetType()), SoodaLinqMethod.Object_GetType);
            dict.Add(MethodOf(() => string.Concat(string.Empty, string.Empty)), SoodaLinqMethod.String_Concat);
            dict.Add(MethodOf(() => LinqUtils.Like(string.Empty, string.Empty)), SoodaLinqMethod.String_Like);
            dict.Add(MethodOf(() => string.Empty.Remove(0)), SoodaLinqMethod.String_Remove);
            dict.Add(MethodOf(() => string.Empty.Replace(string.Empty, string.Empty)), SoodaLinqMethod.String_Replace);
            dict.Add(MethodOf(() => string.Empty.ToLower()), SoodaLinqMethod.String_ToLower);
            dict.Add(MethodOf(() => string.Empty.ToUpper()), SoodaLinqMethod.String_ToUpper);
            dict.Add(MethodOf(() => Math.Abs(0M)), SoodaLinqMethod.Math_Abs);
            dict.Add(MethodOf(() => Math.Abs(0D)), SoodaLinqMethod.Math_Abs);
            dict.Add(MethodOf(() => Math.Abs((short) 0)), SoodaLinqMethod.Math_Abs);
            dict.Add(MethodOf(() => Math.Abs(0)), SoodaLinqMethod.Math_Abs);
            dict.Add(MethodOf(() => Math.Abs(0L)), SoodaLinqMethod.Math_Abs);
            dict.Add(MethodOf(() => Math.Abs((sbyte) 0)), SoodaLinqMethod.Math_Abs);
            dict.Add(MethodOf(() => Math.Abs(0F)), SoodaLinqMethod.Math_Abs);
            dict.Add(MethodOf(() => Math.Acos(0)), SoodaLinqMethod.Math_Acos);
            dict.Add(MethodOf(() => Math.Asin(0)), SoodaLinqMethod.Math_Asin);
            dict.Add(MethodOf(() => Math.Atan(0)), SoodaLinqMethod.Math_Atan);
            dict.Add(MethodOf(() => Math.Cos(0)), SoodaLinqMethod.Math_Cos);
            dict.Add(MethodOf(() => Math.Exp(0)), SoodaLinqMethod.Math_Exp);
            dict.Add(MethodOf(() => Math.Floor(0M)), SoodaLinqMethod.Math_Floor);
            dict.Add(MethodOf(() => Math.Floor(0D)), SoodaLinqMethod.Math_Floor);
            dict.Add(MethodOf(() => Math.Pow(1, 1)), SoodaLinqMethod.Math_Pow);
            dict.Add(MethodOf(() => Math.Round(0M, 0)), SoodaLinqMethod.Math_Round);
            dict.Add(MethodOf(() => Math.Round(0D, 0)), SoodaLinqMethod.Math_Round);
            dict.Add(MethodOf(() => Math.Sign(0M)), SoodaLinqMethod.Math_Sign);
            dict.Add(MethodOf(() => Math.Sign(0D)), SoodaLinqMethod.Math_Sign);
            dict.Add(MethodOf(() => Math.Sign((short) 0)), SoodaLinqMethod.Math_Sign);
            dict.Add(MethodOf(() => Math.Sign(0)), SoodaLinqMethod.Math_Sign);
            dict.Add(MethodOf(() => Math.Sign(0L)), SoodaLinqMethod.Math_Sign);
            dict.Add(MethodOf(() => Math.Sign((sbyte) 0)), SoodaLinqMethod.Math_Sign);
            dict.Add(MethodOf(() => Math.Sign(0F)), SoodaLinqMethod.Math_Sign);
            dict.Add(MethodOf(() => Math.Sin(0)), SoodaLinqMethod.Math_Sin);
            dict.Add(MethodOf(() => Math.Sqrt(0)), SoodaLinqMethod.Math_Sqrt);
            dict.Add(MethodOf(() => Math.Tan(0)), SoodaLinqMethod.Math_Tan);
            _dict = dict;
        }
        SoodaLinqMethod result;
        dict.TryGetValue(Ungeneric(method), out result);
        return result;
    }
}

#endif
