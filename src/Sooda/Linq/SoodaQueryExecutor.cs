//
// Copyright (c) 2010-2014 Piotr Fusik <piotr@fusik.info>
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
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Sooda;
using Sooda.ObjectMapper;
using Sooda.QL;
using Sooda.Schema;

namespace Sooda.Linq
{
    public class SoodaQueryExecutor
    {
        // Caution: this must match first result of SoqlToSqlConverter.GetNextTablePrefix()
        const string DefaultAlias = "t0";

        SoodaTransaction _transaction;
        ClassInfo _classInfo;
        SoodaSnapshotOptions _options;
#if DOTNET4
        SelectExecutor _select = null;
#endif
        bool _distinct = false;
        SoqlBooleanExpression _where = null;
        SoodaOrderBy _orderBy = null;
        int _startIdx = 0;
        int _topCount = -1;
        readonly Dictionary<ParameterExpression, ClassInfo> _param2classInfo = new Dictionary<ParameterExpression, ClassInfo>();
        readonly Dictionary<ParameterExpression, string> _param2alias = new Dictionary<ParameterExpression, string>();
        int _currentPrefix = 0;

        internal SoodaObject GetRef(Type type, object keyValue)
        {
            //if (keyValue == null)
            //    return null;
            return _transaction.GetFactory(type).GetRef(_transaction, keyValue);
        }

        internal static bool IsConstant(Expression expr)
        {
            if (expr == null)
                return true;
            switch (expr.NodeType)
            {
                case ExpressionType.Constant:
                    return true;
                case ExpressionType.Parameter:
                    return false;
                case ExpressionType.UnaryPlus:
                case ExpressionType.Negate:
                case ExpressionType.NegateChecked:
                case ExpressionType.Not:
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                case ExpressionType.ArrayLength:
                case ExpressionType.Quote:
                case ExpressionType.TypeAs:
                    return IsConstant(((UnaryExpression) expr).Operand);
                case ExpressionType.TypeIs:
                    return IsConstant(((TypeBinaryExpression) expr).Expression);
                case ExpressionType.Add:
                case ExpressionType.AddChecked:
                case ExpressionType.Subtract:
                case ExpressionType.SubtractChecked:
                case ExpressionType.Multiply:
                case ExpressionType.MultiplyChecked:
                case ExpressionType.Divide:
                case ExpressionType.Modulo:
                case ExpressionType.Power:
                case ExpressionType.And:
                case ExpressionType.AndAlso:
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                case ExpressionType.ExclusiveOr:
                case ExpressionType.LessThan:
                case ExpressionType.LessThanOrEqual:
                case ExpressionType.GreaterThan:
                case ExpressionType.GreaterThanOrEqual:
                case ExpressionType.Equal:
                case ExpressionType.NotEqual:
                case ExpressionType.Coalesce:
                case ExpressionType.ArrayIndex:
                case ExpressionType.LeftShift:
                case ExpressionType.RightShift:
                    BinaryExpression be = (BinaryExpression) expr;
                    return IsConstant(be.Left) && IsConstant(be.Right);
                case ExpressionType.Conditional:
                    ConditionalExpression ce = (ConditionalExpression) expr;
                    return IsConstant(ce.Test) && IsConstant(ce.IfTrue) && IsConstant(ce.IfFalse);
                case ExpressionType.MemberAccess:
                    return IsConstant(((MemberExpression) expr).Expression);
                case ExpressionType.Call:
                    MethodCallExpression mc = (MethodCallExpression) expr;
                    return IsConstant(mc.Object) && mc.Arguments.All(IsConstant);
                case ExpressionType.Lambda:
                    return IsConstant(((LambdaExpression) expr).Body);
                case ExpressionType.New:
                    return ((NewExpression) expr).Arguments.All(IsConstant);
                case ExpressionType.NewArrayBounds:
                case ExpressionType.NewArrayInit:
                    return ((NewArrayExpression) expr).Expressions.All(IsConstant);
                case ExpressionType.Invoke:
                    InvocationExpression ie = (InvocationExpression) expr;
                    return IsConstant(ie.Expression) && ie.Arguments.All(IsConstant);
                case ExpressionType.MemberInit:
                case ExpressionType.ListInit:
                default:
                    throw new NotSupportedException(expr.NodeType.ToString());
            }
        }

        static SoqlExpression FoldConstant(Expression expr)
        {
            if (!IsConstant(expr))
                return null;

            object value;
            ConstantExpression constExpr = expr as ConstantExpression;
            if (constExpr != null)
                value = constExpr.Value;
            else
                value = Expression.Lambda(expr).Compile().DynamicInvoke(null);

            SoodaObject so = value as SoodaObject;
            if (so != null)
                value = so.GetPrimaryKeyValue();

            if (value == null)
                return new SoqlNullLiteral();
            if (value is bool)
                return (bool) value ? SoqlBooleanLiteralExpression.True : SoqlBooleanLiteralExpression.False;

            return new SoqlLiteralExpression(value);
        }

        SoqlBooleanExpression TranslateAnd(BinaryExpression expr)
        {
            return TranslateBoolean(expr.Left).And(TranslateBoolean(expr.Right));
        }

        SoqlBooleanExpression TranslateOr(BinaryExpression expr)
        {
            return TranslateBoolean(expr.Left).Or(TranslateBoolean(expr.Right));
        }

        SoqlBinaryExpression TranslateBinary(BinaryExpression expr, SoqlBinaryOperator op)
        {
            return new SoqlBinaryExpression(TranslateExpression(expr.Left), TranslateExpression(expr.Right), op);
        }

        SoqlExpression TranslateConvert(UnaryExpression expr)
        {
            if (expr.Operand.Type == typeof(object)
             || Nullable.GetUnderlyingType(expr.Type) == expr.Operand.Type // T -> Nullable<T>
             || (expr.Type == typeof(double) && expr.Operand.Type == typeof(int)))
                return TranslateExpression(expr.Operand);
            throw new NotSupportedException("Convert " + expr.Operand.Type + " to " + expr.Type);
        }

        SoqlBooleanExpression TranslateEqualsLiteral(SoqlExpression left, SoqlExpression right, bool notEqual)
        {
            ISoqlConstantExpression rightConst = right as ISoqlConstantExpression;
            if (rightConst != null)
            {
                object value = rightConst.GetConstantValue();

                // left == null -> left IS NULL
                // left != null -> left IS NOT NULL
                if (value == null || value == DBNull.Value)
                    return new SoqlBooleanIsNullExpression(left, notEqual);

                if (value is bool)
                {
                    SoqlBooleanExpression leftBool = left as SoqlBooleanExpression;
                    if (leftBool != null)
                    {
                        // left == true, left != false -> left
                        if ((bool) value ^ notEqual)
                            return leftBool;
                        // left == false, left != true -> NOT(left)
                        return new SoqlBooleanNegationExpression(leftBool);
                    }
                }
            }
            return null;
        }

        SoqlBooleanExpression TranslateRelational(BinaryExpression expr, SoqlRelationalOperator op)
        {
            SoqlExpression left = TranslateExpression(expr.Left);
            SoqlExpression right = TranslateExpression(expr.Right);
            SoqlBooleanExpression result;

            switch (op)
            {
                case SoqlRelationalOperator.Equal:
                    result = TranslateEqualsLiteral(left, right, false) ?? TranslateEqualsLiteral(right, left, false);
                    break;
                case SoqlRelationalOperator.NotEqual:
                    result = TranslateEqualsLiteral(left, right, true) ?? TranslateEqualsLiteral(right, left, true);
                    break;
                default:
                    result = null;
                    break;
            }
            return result ?? new SoqlBooleanRelationalExpression(left, right, op);
        }

        SoqlExpression TranslateConditional(ConditionalExpression expr)
        {
            SoqlBooleanExpression condition = TranslateBoolean(expr.Test);
            SoqlBooleanLiteralExpression constCondition = condition as SoqlBooleanLiteralExpression;
            if (constCondition != null)
                return TranslateExpression(constCondition.Value ? expr.IfTrue : expr.IfFalse);
            return new SoqlConditionalExpression(condition, TranslateExpression(expr.IfTrue), TranslateExpression(expr.IfFalse));
        }

        SoqlPathExpression TranslateParameter(ParameterExpression pe)
        {
            // Don't use DefaultAlias if not needed.
            // This is necessary because OrderBy clauses don't handle table aliases.
            if (_param2alias.Count == 0)
                return null;

            string alias;
            if (_param2alias.TryGetValue(pe, out alias))
                return new SoqlPathExpression(alias);
            return new SoqlPathExpression(DefaultAlias);
        }

        SoqlPathExpression TranslateToPathExpression(Expression expr)
        {
            if (expr.NodeType == ExpressionType.Parameter)
                return TranslateParameter((ParameterExpression) expr);
            return (SoqlPathExpression) TranslateExpression(expr);
        }

        SoqlExpression TranslateMember(MemberExpression expr)
        {
            string name = expr.Member.Name;
            Type t = expr.Member.DeclaringType;

            if (expr.Expression != null)
            {
                // non-static members

                // x.SoodaField -> SoqlPathExpression
                if (expr.Expression.NodeType == ExpressionType.Parameter)
                    return new SoqlPathExpression(TranslateParameter((ParameterExpression) expr.Expression), name);

                // x.GetType().Name -> SoqlSoodaClassExpression
                if (t == typeof(MemberInfo) && name == "Name" && expr.Expression.NodeType == ExpressionType.Call)
                {
                    MethodCallExpression mc = (MethodCallExpression) expr.Expression;
                    if (SoodaLinqMethodDictionary.Get(mc.Method) == SoodaLinqMethod.Object_GetType)
                        return new SoqlSoodaClassExpression(TranslateToPathExpression(mc.Object));
                }

                SoqlExpression parent = TranslateExpression(expr.Expression);

                if (typeof(INullable).IsAssignableFrom(t))
                {
                    if (name == "Value")
                        return parent;
                    if (name == "IsNull")
                        return new SoqlBooleanIsNullExpression(parent, false);
                }

                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    if (name == "Value")
                        return parent;
                    if (name == "HasValue")
                        return new SoqlBooleanIsNullExpression(parent, true);
                }

                if (t == typeof(DateTime))
                {
                    switch (name)
                    {
                        case "Day":
                            return new SoqlFunctionCallExpression("day", parent);
                        case "Month":
                            return new SoqlFunctionCallExpression("month", parent);
                        case "Year":
                            return new SoqlFunctionCallExpression("year", parent);
                        default:
                            break;
                    }
                }

                SoqlPathExpression parentPath = parent as SoqlPathExpression;
                if (parentPath != null && expr.Member.MemberType == MemberTypes.Property) {
                    // x.SoodaField1.SoodaField2 -> SoqlPathExpression
                    if (t.IsSubclassOf(typeof(SoodaObject)))
                    {
                        if (!FindClassInfo(expr.Expression).ContainsField(name))
                            throw new NotSupportedException(name + " is not a Sooda field");
                        return new SoqlPathExpression(parentPath, name);
                    }

                    // x.SoodaCollection.Count -> SoqlCountExpression
                    if (t == typeof(SoodaObjectCollectionWrapper) && name == "Count")
                        return new SoqlCountExpression(parentPath.Left, parentPath.PropertyName);
                }
            }

            throw new NotSupportedException(t.FullName + "." + name);
        }

        SoqlContainsExpression TranslateContains(Expression haystack, SoqlExpression needle)
        {
            MemberExpression me = haystack as MemberExpression;
            if (me == null)
                throw new NotSupportedException();
            return new SoqlContainsExpression(TranslateToPathExpression(me.Expression), me.Member.Name, needle);
        }

        SoqlBooleanExpression TranslateCollectionAny(MethodCallExpression mc, SoodaLinqMethod method)
        {
            string className = mc.Method.GetGenericArguments()[0].Name;
            string alias;
            SoqlBooleanExpression where;
            if (method == SoodaLinqMethod.Enumerable_Any)
            {
                alias = string.Empty;
                where = SoqlBooleanLiteralExpression.True;
            }
            else
            {
                alias = "any" + _currentPrefix++;
                LambdaExpression lambda = (LambdaExpression) mc.Arguments[1];
                ParameterExpression param = lambda.Parameters.Single();
                _param2classInfo[param] = _transaction.Schema.FindClassByName(className);
                _param2alias[param] = alias;
                where = TranslateBoolean(lambda.Body);
                _param2classInfo.Remove(param);
                _param2alias.Remove(param);
                if (method == SoodaLinqMethod.Enumerable_All)
                    where = new SoqlBooleanNegationExpression(where);
            }

            SoqlQueryExpression query = new SoqlQueryExpression();
            query.From.Add(className);
            query.FromAliases.Add(alias);
            query.WhereClause = where;
            return TranslateContains(mc.Arguments[0], query);
        }

        SoqlBooleanInExpression TranslateCollectionContains(Expression haystack, Expression needle)
        {
            IEnumerable haystack2;
            SoqlExpression literal = FoldConstant(haystack);
            if (literal != null)
                haystack2 = (IEnumerable) ((SoqlLiteralExpression) literal).GetConstantValue();
            else if (haystack.NodeType == ExpressionType.NewArrayInit)
                haystack2 = ((NewArrayExpression) haystack).Expressions.Select(e => TranslateExpression(e));
            else
                throw new NotSupportedException(haystack.NodeType.ToString());

            if (needle.NodeType == ExpressionType.Convert && needle.Type == typeof(object)) // IList.Contains(object)
                needle = ((UnaryExpression) needle).Operand;

            return new SoqlBooleanInExpression(TranslateExpression(needle), haystack2);
        }

        ClassInfo FindClassInfo(Expression expr)
        {
            string className = expr.Type.Name;
            ClassInfo classInfo = _transaction.Schema.FindClassByName(className);
            if (classInfo == null)
                throw new NotSupportedException("Class " + className + " not found in database schema");
            return classInfo;
        }

        SoqlExpression TranslateCall(MethodCallExpression mc)
        {
            switch (SoodaLinqMethodDictionary.Get(mc.Method))
            {
                case SoodaLinqMethod.Enumerable_All:
                    return new SoqlBooleanNegationExpression(TranslateCollectionAny(mc, SoodaLinqMethod.Enumerable_All));
                case SoodaLinqMethod.Enumerable_Any:
                    return TranslateCollectionAny(mc, SoodaLinqMethod.Enumerable_Any);
                case SoodaLinqMethod.Enumerable_AnyFiltered:
                    return TranslateCollectionAny(mc, SoodaLinqMethod.Enumerable_AnyFiltered);
                case SoodaLinqMethod.Enumerable_Contains:
                    return TranslateCollectionContains(mc.Arguments[0], mc.Arguments[1]);
                case SoodaLinqMethod.Enumerable_Count:
                    SoqlPathExpression parentPath = (SoqlPathExpression) TranslateExpression(mc.Arguments[0]);
                    return new SoqlCountExpression(parentPath.Left, parentPath.PropertyName);
                case SoodaLinqMethod.ICollection_Contains:
                    return TranslateCollectionContains(mc.Object, mc.Arguments[0]);
                case SoodaLinqMethod.String_Concat:
                    return new SoqlFunctionCallExpression("concat", TranslateExpression(mc.Arguments[0]), TranslateExpression(mc.Arguments[1]));
                case SoodaLinqMethod.String_Like:
                    return new SoqlBooleanRelationalExpression(
                        TranslateExpression(mc.Arguments[0]),
                        TranslateExpression(mc.Arguments[1]),
                        SoqlRelationalOperator.Like);
                case SoodaLinqMethod.String_Remove:
                    return new SoqlFunctionCallExpression("left", TranslateExpression(mc.Object), TranslateExpression(mc.Arguments[0]));
                case SoodaLinqMethod.String_Replace:
                    SoqlExpressionCollection parameters = new SoqlExpressionCollection {
                        TranslateExpression(mc.Object),
                        TranslateExpression(mc.Arguments[0]),
                        TranslateExpression(mc.Arguments[1])
                    };
                    return new SoqlFunctionCallExpression("replace", parameters);
                case SoodaLinqMethod.String_ToLower:
                    return new SoqlFunctionCallExpression("lower", TranslateExpression(mc.Object));
                case SoodaLinqMethod.String_ToUpper:
                    return new SoqlFunctionCallExpression("upper", TranslateExpression(mc.Object));
                case SoodaLinqMethod.Math_Abs:
                    return new SoqlFunctionCallExpression("abs", TranslateExpression(mc.Arguments[0]));
                case SoodaLinqMethod.Math_Acos:
                    return new SoqlFunctionCallExpression("acos", TranslateExpression(mc.Arguments[0]));
                case SoodaLinqMethod.Math_Asin:
                    return new SoqlFunctionCallExpression("asin", TranslateExpression(mc.Arguments[0]));
                case SoodaLinqMethod.Math_Atan:
                    return new SoqlFunctionCallExpression("atan", TranslateExpression(mc.Arguments[0]));
                case SoodaLinqMethod.Math_Cos:
                    return new SoqlFunctionCallExpression("cos", TranslateExpression(mc.Arguments[0]));
                case SoodaLinqMethod.Math_Exp:
                    return new SoqlFunctionCallExpression("exp", TranslateExpression(mc.Arguments[0]));
                case SoodaLinqMethod.Math_Floor:
                    return new SoqlFunctionCallExpression("floor", TranslateExpression(mc.Arguments[0]));
                case SoodaLinqMethod.Math_Pow:
                    return new SoqlFunctionCallExpression("power", TranslateExpression(mc.Arguments[0]), TranslateExpression(mc.Arguments[1]));
                case SoodaLinqMethod.Math_Round:
                    return new SoqlFunctionCallExpression("round", TranslateExpression(mc.Arguments[0]), TranslateExpression(mc.Arguments[1]));
                case SoodaLinqMethod.Math_Sign:
                    return new SoqlFunctionCallExpression("sign", TranslateExpression(mc.Arguments[0]));
                case SoodaLinqMethod.Math_Sin:
                    return new SoqlFunctionCallExpression("sin", TranslateExpression(mc.Arguments[0]));
                case SoodaLinqMethod.Math_Sqrt:
                    return new SoqlFunctionCallExpression("sqrt", TranslateExpression(mc.Arguments[0]));
                case SoodaLinqMethod.Math_Tan:
                    return new SoqlFunctionCallExpression("tan", TranslateExpression(mc.Arguments[0]));
                case SoodaLinqMethod.SoodaObject_GetPrimaryKeyValue:
                    return new SoqlPathExpression(TranslateToPathExpression(mc.Object), FindClassInfo(mc.Object).GetPrimaryKeyFields().Single().Name);
                case SoodaLinqMethod.SoodaObject_GetLabel:
                    string labelField = FindClassInfo(mc.Object).GetLabel();
                    if (labelField == null)
                        return new SoqlNullLiteral();
                    return new SoqlPathExpression(TranslateToPathExpression(mc.Object), labelField);
                default:
                    break;
            }

            Type t = mc.Method.DeclaringType;

            Type cwg = t.BaseType;
            if (cwg != null && cwg.IsGenericType && cwg.GetGenericTypeDefinition() == typeof(SoodaObjectCollectionWrapperGeneric<>) && mc.Method.Name == "Contains")
            {
                if (IsConstant(mc.Object))
                {
                    // ConstSoodaCollection.Contains(expr) -> SoqlBooleanInExpression
                    return TranslateCollectionContains(mc.Object, mc.Arguments[0]);
                }
                // x.SoodaCollection.Contains(expr) -> SoqlContainsExpression
                return TranslateContains(mc.Object, TranslateExpression(mc.Arguments[0]));
            }

            throw new NotSupportedException(t.FullName + "." + mc.Method.Name);
        }

        SoqlExpression TranslateToFunction(string function, BinaryExpression expr)
        {
            return new SoqlFunctionCallExpression(function, TranslateExpression(expr.Left), TranslateExpression(expr.Right));
        }

        SoqlBooleanExpression TranslateTypeIs(TypeBinaryExpression expr)
        {
            SoqlPathExpression path = TranslateToPathExpression(expr.Expression);
            Type type = expr.TypeOperand;
            if (type != typeof(object)) // x is object -> x IS NOT NULL
            {
                if (!type.IsSubclassOf(typeof(SoodaObject)))
                    throw new NotSupportedException("'is' operator supported only for Sooda classes and object");
                SchemaInfo schema = _transaction.Schema;
                ClassInfo classInfo = schema.FindClassByName(type.Name);
                if (classInfo == null)
                    throw new NotSupportedException("is " + type.Name);

                SoqlBooleanExpression result = Soql.ClassRestriction(path, schema, classInfo);
                if (result != null)
                    return result;
            }

            if (expr.Expression.NodeType == ExpressionType.Parameter)
            {
                // path is probably not valid SoqlPathExpression.
                // Fortunately, primary keys should be non-null.
                return SoqlBooleanLiteralExpression.True;
            }

            // path IS NOT NULL
            return new SoqlBooleanIsNullExpression(path, true);
        }

        internal SoqlExpression TranslateExpression(Expression expr)
        {
            SoqlExpression literal = FoldConstant(expr);
            if (literal != null)
                return literal;

            switch (expr.NodeType)
            {
                case ExpressionType.Parameter:
                    ParameterExpression pe = (ParameterExpression) expr;
                    ClassInfo classInfo;
                    if (!_param2classInfo.TryGetValue(pe, out classInfo))
                        throw new NotSupportedException();
                    return new SoqlPathExpression(TranslateParameter(pe), classInfo.GetPrimaryKeyFields().Single().Name);
                case ExpressionType.MemberAccess:
                    return TranslateMember((MemberExpression) expr);
                case ExpressionType.Add:
                    return TranslateBinary((BinaryExpression) expr, SoqlBinaryOperator.Add);
                case ExpressionType.Subtract:
                    return TranslateBinary((BinaryExpression) expr, SoqlBinaryOperator.Sub);
                case ExpressionType.Multiply:
                    return TranslateBinary((BinaryExpression) expr, SoqlBinaryOperator.Mul);
                case ExpressionType.Divide:
                    return TranslateBinary((BinaryExpression) expr, SoqlBinaryOperator.Div);
                case ExpressionType.Modulo:
                    return TranslateBinary((BinaryExpression) expr, SoqlBinaryOperator.Mod);
                case ExpressionType.Power:
                    return TranslateToFunction("power", (BinaryExpression) expr);
                case ExpressionType.Negate:
                    return new SoqlUnaryNegationExpression(TranslateExpression(((UnaryExpression) expr).Operand));
                case ExpressionType.Convert:
                    return TranslateConvert((UnaryExpression) expr);
                case ExpressionType.And:
                case ExpressionType.AndAlso:
                    return TranslateAnd((BinaryExpression) expr);
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                    return TranslateOr((BinaryExpression) expr);
                case ExpressionType.Not:
                    return new SoqlBooleanNegationExpression(TranslateBoolean(((UnaryExpression) expr).Operand));
                case ExpressionType.Coalesce:
                    return TranslateToFunction("coalesce", (BinaryExpression) expr);
                case ExpressionType.Equal:
                    return TranslateRelational((BinaryExpression) expr, SoqlRelationalOperator.Equal);
                case ExpressionType.NotEqual:
                    return TranslateRelational((BinaryExpression) expr, SoqlRelationalOperator.NotEqual);
                case ExpressionType.LessThan:
                    return TranslateRelational((BinaryExpression) expr, SoqlRelationalOperator.Less);
                case ExpressionType.LessThanOrEqual:
                    return TranslateRelational((BinaryExpression) expr, SoqlRelationalOperator.LessOrEqual);
                case ExpressionType.GreaterThan:
                    return TranslateRelational((BinaryExpression) expr, SoqlRelationalOperator.Greater);
                case ExpressionType.GreaterThanOrEqual:
                    return TranslateRelational((BinaryExpression) expr, SoqlRelationalOperator.GreaterOrEqual);
                case ExpressionType.Conditional:
                    return TranslateConditional((ConditionalExpression) expr);
                case ExpressionType.Call:
                    return TranslateCall((MethodCallExpression) expr);
                case ExpressionType.TypeIs:
                    return TranslateTypeIs((TypeBinaryExpression) expr);
                default:
                    throw new NotSupportedException(expr.NodeType.ToString());
            }
        }

        SoqlBooleanExpression TranslateBoolean(Expression expr)
        {
            SoqlExpression ql = TranslateExpression(expr);
            return ql as SoqlBooleanExpression ?? new SoqlBooleanRelationalExpression(ql, SoqlBooleanLiteralExpression.True, SoqlRelationalOperator.Equal);
        }

        LambdaExpression GetLambda(MethodCallExpression mc)
        {
            LambdaExpression lambda = (LambdaExpression) ((UnaryExpression) mc.Arguments[1]).Operand;
            _param2classInfo[lambda.Parameters[0]] = _classInfo;
            return lambda;
        }

        SoqlQueryExpression CreateSoqlQuery()
        {
            SoqlQueryExpression query = new SoqlQueryExpression();
            query.StartIdx = _startIdx;
            query.PageCount = _topCount;
            query.From.Add(_classInfo.Name);
            // string.Empty will result in DefaultAlias.
            // Do NOT specify DefaultAlias here, because GetNextTablePrefix() could assign DefaultAlias to other tables.
            query.FromAliases.Add(string.Empty);
            query.WhereClause = _where;
            if (_orderBy != null)
                query.SetOrderBy(_orderBy);
            return query;
        }

        void SkipTakeNotSupported()
        {
            if (_startIdx > 0 || _topCount >= 0)
            {
                SoqlPathExpression needle = new SoqlPathExpression(_classInfo.GetPrimaryKeyFields().Single().Name);
                SoqlExpressionCollection haystack = new SoqlExpressionCollection();
                haystack.Add(CreateSoqlQuery());
                _where = new SoqlBooleanInExpression(needle, haystack);
                _startIdx = 0;
                _topCount = -1;
                // _orderBy must be in both the subquery and the outer query
            }
        }

        void Select(MethodCallExpression mc)
        {
#if DOTNET4
            if (_select != null)
                throw new NotSupportedException("Chaining Select()s not supported");
            _select = new SelectExecutor(this);
            _select.Process(GetLambda(mc));
#else
            throw new NotImplementedException("Select() requires Sooda for .NET 4, this is .NET 3.5");
#endif
        }

        void Where(SoqlBooleanExpression where)
        {
            _where = _where == null ? where : _where.And(where);
        }

        void Where(MethodCallExpression mc)
        {
            SkipTakeNotSupported();
            Where(TranslateBoolean(GetLambda(mc).Body));
        }

        void Reverse()
        {
            SkipTakeNotSupported();
            if (_orderBy != null)
            {
                SortOrder[] sortOrders = _orderBy.SortOrders;
                for (int i = 0; i < sortOrders.Length; i++)
                    sortOrders[i] = sortOrders[i] == SortOrder.Descending ? SortOrder.Ascending : SortOrder.Descending;
                _orderBy = new SoodaOrderBy(_orderBy.OrderByExpressions, sortOrders);
            }
            else
            {
                // There was no order, so order by primary keys descending.
                // This should do the trick for SQL Server if the primary keys are clustered.
                Sooda.Schema.FieldInfo[] pks = _classInfo.GetPrimaryKeyFields();
                string[] columnNames = new string[pks.Length];
                SortOrder[] sortOrders = new SortOrder[pks.Length];
                for (int i = 0; i < pks.Length; i++)
                {
                    columnNames[i] = pks[i].Name;
                    sortOrders[i] = SortOrder.Descending;
                }
                _orderBy = new SoodaOrderBy(columnNames, sortOrders);
            }
        }

        void Take(int count)
        {
            if (_topCount < 0 || _topCount > count)
                _topCount = count;
        }

        static bool IsSameOrSubclassOf(ClassInfo subClass, ClassInfo baseClass)
        {
            while (subClass.Name != baseClass.Name)
            {
                subClass = subClass.InheritsFromClass;
                if (subClass == null)
                    return false;
            }
            return true;
        }

        void OfType(Type type)
        {
            // x.OfType<object>() -> x
            // x.OfType<SoodaObject>() -> x
            if (type != typeof(object) && type != typeof(SoodaObject))
            {
                if (!type.IsSubclassOf(typeof(SoodaObject)))
                    throw new NotSupportedException("OfType() supported only for Sooda classes and object");
                ClassInfo classInfo = _transaction.Schema.FindClassByName(type.Name);
                if (classInfo == null)
                    throw new NotSupportedException("OfType() supported only for Sooda classes and object");

                if (IsSameOrSubclassOf(_classInfo, classInfo))
                {
                    // x.OfType<X>() -> x
                    // x.OfType<BaseClass>() -> x
                }
                else if (IsSameOrSubclassOf(classInfo, _classInfo))
                {
                    // x.OfType<SubClass>() -> from SubClass ...
                    _classInfo = classInfo;
                }
                else
                    _where = SoqlBooleanLiteralExpression.False;
            }
        }

        SoqlBooleanExpression TranslateSubquery(Expression expr)
        {
            SkipTakeNotSupported();

            if (expr.NodeType == ExpressionType.Constant)
            {
                // e.g. Contact.Linq().Union(new Contact[] { Contact.Mary })
                // TODO: NOT if .Union(Contact.Linq())
                SoqlPathExpression needle = new SoqlPathExpression(_classInfo.GetPrimaryKeyFields().Single().Name);
                IEnumerable haystack = (IEnumerable) ((ConstantExpression) expr).Value;
                return new SoqlBooleanInExpression(needle, haystack);
            }

            // TODO: compare _transaction, _classInfo, _options
            // TODO: OfType?
            SoqlBooleanExpression thisWhere = _where;
            SoodaOrderBy thisOrderBy = _orderBy;
            _where = null;
            TranslateQuery(expr);
            SkipTakeNotSupported();
            SoqlBooleanExpression thatWhere = _where;
            _where = thisWhere;
            _orderBy = thisOrderBy;
            return thatWhere;
        }

        void TranslateQuery(Expression expr)
        {
            SoqlBooleanExpression thatWhere;
            switch (expr.NodeType)
            {
                case ExpressionType.Constant:
                     ISoodaQuerySource source = (ISoodaQuerySource) ((ConstantExpression) expr).Value;
                    _transaction = source.Transaction;
                    _classInfo = source.ClassInfo;
                    _options = source.Options;
                    _where = source.Where;
                    break;

                case ExpressionType.Call:
                    MethodCallExpression mc = (MethodCallExpression) expr;
                    SoodaLinqMethod method = SoodaLinqMethodDictionary.Get(mc.Method);
                    TranslateQuery(mc.Arguments[0]);
                    switch (method)
                    {
                        case SoodaLinqMethod.Queryable_Select:
                        case SoodaLinqMethod.Queryable_SelectIndexed:
                            Select(mc);
                            break;

                        case SoodaLinqMethod.Queryable_Where:
                            Where(mc);
                            break;

                        case SoodaLinqMethod.Queryable_OrderBy:
                        case SoodaLinqMethod.Queryable_OrderByDescending:
                        case SoodaLinqMethod.Queryable_ThenBy:
                        case SoodaLinqMethod.Queryable_ThenByDescending:
                            SoqlExpression orderBy = TranslateExpression(GetLambda(mc).Body);
                            SkipTakeNotSupported();
                            switch (method)
                            {
                                case SoodaLinqMethod.Queryable_OrderBy:
                                    _orderBy = new SoodaOrderBy(orderBy, SortOrder.Ascending, _orderBy);
                                    break;
                                case SoodaLinqMethod.Queryable_OrderByDescending:
                                    _orderBy = new SoodaOrderBy(orderBy, SortOrder.Descending, _orderBy);
                                    break;
                                case SoodaLinqMethod.Queryable_ThenBy:
                                    _orderBy = new SoodaOrderBy(_orderBy, orderBy, SortOrder.Ascending);
                                    break;
                                case SoodaLinqMethod.Queryable_ThenByDescending:
                                    _orderBy = new SoodaOrderBy(_orderBy, orderBy, SortOrder.Descending);
                                    break;
                            }
                            break;
                        case SoodaLinqMethod.Queryable_Reverse:
                            Reverse();
                            break;

                        case SoodaLinqMethod.Queryable_Skip:
                            {
                                int count = (int) ((ConstantExpression) mc.Arguments[1]).Value;
                                if (count > 0)
                                {
                                    if (_startIdx + count < 0) // int overflow
                                        SkipTakeNotSupported();
                                    _startIdx += count;
                                    if (_topCount > 0)
                                        _topCount = Math.Max(_topCount - count, 0);
                                }
                            }
                            break;

                        case SoodaLinqMethod.Queryable_Take:
                            {
                                int count = (int) ((ConstantExpression) mc.Arguments[1]).Value;
                                if (count < 0)
                                    count = 0;
                                Take(count);
                            }
                            break;

                        case SoodaLinqMethod.Queryable_Distinct:
#if DOTNET4
                            if (_select != null)
                            {
                                SkipTakeNotSupported();
                                _distinct = true;
                            }
#endif
                            break;

                        case SoodaLinqMethod.Queryable_OfType:
                            OfType(mc.Method.GetGenericArguments()[0]);
                            break;

                        case SoodaLinqMethod.Queryable_Except:
                            thatWhere = TranslateSubquery(mc.Arguments[1]);
                            thatWhere = thatWhere == null ? (SoqlBooleanExpression) SoqlBooleanLiteralExpression.False : new SoqlBooleanNegationExpression(thatWhere);
                            Where(thatWhere);
                            break;
                        case SoodaLinqMethod.Queryable_Intersect:
                            thatWhere = TranslateSubquery(mc.Arguments[1]);
                            if (thatWhere != null)
                                Where(thatWhere);
                            break;
                        case SoodaLinqMethod.Queryable_Union:
                            thatWhere = TranslateSubquery(mc.Arguments[1]);
                            if (_where != null)
                                _where = thatWhere == null ? null : _where.Or(thatWhere);
                            break;

                        default:
                            throw new NotSupportedException(mc.Method.Name);
                    }
                    break;

                default:
                    throw new NotSupportedException(expr.NodeType.ToString());
            }
        }

        internal IDataReader ExecuteQuery(IEnumerable<SoqlExpression> columns)
        {
            SoqlQueryExpression query = CreateSoqlQuery();
            foreach (SoqlExpression column in columns)
            {
                query.SelectExpressions.Add(column);
                query.SelectAliases.Add(string.Empty);
            }
            query.Distinct = _distinct;
            SoodaDataSource ds = _transaction.OpenDataSource(_classInfo.GetDataSource());
            return ds.ExecuteQuery(query, _transaction.Schema);
        }

        internal static List<T> SelectOneColumn<T>(IDataReader r)
        {
            List<T> list = new List<T>();
            while (r.Read())
            {
                object value = r.GetValue(0);
                if (value == DBNull.Value)
                    value = null;
                else if (typeof(T) == typeof(bool) && value is int)
                    value = (int) value != 0;
                list.Add((T) value);
            }
            return list;
        }

        IList GetList()
        {
#if DOTNET4
            if (_select != null)
                return _select.GetList();
#endif
            return new SoodaObjectListSnapshot(_transaction, new SoodaWhereClause(_where), _orderBy, _startIdx, _topCount, _options, _classInfo);
        }

        object Single(int topCount, bool orDefault)
        {
            Take(topCount);
            IList list = GetList();
            if (list.Count == 1)
                return list[0];
            if (orDefault && list.Count == 0)
                return null;
            throw new InvalidOperationException("Found " + list.Count + " matches");
        }

        object ExecuteScalar(SoqlExpression expr, string function)
        {
            SkipTakeNotSupported();
            _orderBy = null;

            SoqlExpression selector = new SoqlFunctionCallExpression(function, expr);
            using (IDataReader r = ExecuteQuery(new SoqlExpression[] { selector }))
            {
                if (!r.Read())
                    throw new SoodaObjectNotFoundException();
                object result = r.GetValue(0);
                if (result != DBNull.Value)
                    return result;
                return null;
            }
        }

        object ExecuteScalar(MethodCallExpression mc, string function)
        {
            TranslateQuery(mc.Arguments[0]);
            SoqlExpression expr;
#if DOTNET4
            if (mc.Arguments.Count == 1)
            {
                if (_select == null)
                    throw new NotSupportedException("Cannot aggregate SoodaObjects");
                expr = _select.GetSingleColumnExpression();
            }
            else
#endif
            {
                expr = TranslateExpression(GetLambda(mc).Body);
            }
            return ExecuteScalar(expr, function);
        }

        int Count()
        {
#if CACHE_LINQ_COUNT
            return GetList().Count;
#else
            return (int) ExecuteScalar(new SoqlAsteriskExpression(), "count");
#endif
        }

        object ThrowEmptyAggregate()
        {
            throw new InvalidOperationException("Aggregate on an empty collection");
        }

        object ExecuteAvg(MethodCallExpression mc)
        {
            object result = ExecuteScalar(mc, "avg");
            if (result is int || result is long)
                return Convert.ToDouble(result);
            return result;
        }

        internal object Execute(Expression expr)
        {
#if DOTNET4
            _select = null;
#endif
            _where = null;
            _orderBy = null;
            _startIdx = 0;
            _topCount = -1;

            MethodCallExpression mc = expr as MethodCallExpression;
            if (mc != null)
            {
                switch (SoodaLinqMethodDictionary.Get(mc.Method))
                {
                    case SoodaLinqMethod.Queryable_All:
                        TranslateQuery(mc.Arguments[0]);
                        SkipTakeNotSupported();
                        Where(new SoqlBooleanNegationExpression(TranslateBoolean(GetLambda(mc).Body)));
                        Take(1);
                        return Count() == 0;
                    case SoodaLinqMethod.Queryable_Any:
                        TranslateQuery(mc.Arguments[0]);
                        Take(1);
                        return Count() > 0;
                    case SoodaLinqMethod.Queryable_AnyFiltered:
                        TranslateQuery(mc.Arguments[0]);
                        Where(mc);
                        Take(1);
                        return Count() > 0;
                    case SoodaLinqMethod.Queryable_Contains:
                        TranslateQuery(mc.Arguments[0]);
                        SkipTakeNotSupported();
                        SoqlBooleanExpression where = new SoqlBooleanRelationalExpression(
                            new SoqlPathExpression(_classInfo.GetPrimaryKeyFields().Single().Name),
                            FoldConstant(mc.Arguments[1]),
                            SoqlRelationalOperator.Equal);
                        Where(where);
                        Take(1);
                        return Count() > 0;
                    case SoodaLinqMethod.Queryable_Count:
                        TranslateQuery(mc.Arguments[0]);
                        return Count();
                    case SoodaLinqMethod.Queryable_CountFiltered:
                        TranslateQuery(mc.Arguments[0]);
                        Where(mc);
                        return Count();

                    case SoodaLinqMethod.Queryable_First:
                        TranslateQuery(mc.Arguments[0]);
                        return Single(1, false);
                    case SoodaLinqMethod.Queryable_FirstFiltered:
                        TranslateQuery(mc.Arguments[0]);
                        Where(mc);
                        return Single(1, false);
                    case SoodaLinqMethod.Queryable_FirstOrDefault:
                        TranslateQuery(mc.Arguments[0]);
                        return Single(1, true);
                    case SoodaLinqMethod.Queryable_FirstOrDefaultFiltered:
                        TranslateQuery(mc.Arguments[0]);
                        Where(mc);
                        return Single(1, true);
                    case SoodaLinqMethod.Queryable_Last:
                        TranslateQuery(mc.Arguments[0]);
                        Reverse();
                        return Single(1, false);
                    case SoodaLinqMethod.Queryable_LastFiltered:
                        TranslateQuery(mc.Arguments[0]);
                        Where(mc);
                        Reverse();
                        return Single(1, false);
                    case SoodaLinqMethod.Queryable_LastOrDefault:
                        TranslateQuery(mc.Arguments[0]);
                        Reverse();
                        return Single(1, true);
                    case SoodaLinqMethod.Queryable_LastOrDefaultFiltered:
                        TranslateQuery(mc.Arguments[0]);
                        Where(mc);
                        Reverse();
                        return Single(1, true);
                    case SoodaLinqMethod.Queryable_Single:
                        TranslateQuery(mc.Arguments[0]);
                        return Single(2, false);
                    case SoodaLinqMethod.Queryable_SingleFiltered:
                        TranslateQuery(mc.Arguments[0]);
                        Where(mc);
                        return Single(2, false);
                    case SoodaLinqMethod.Queryable_SingleOrDefault:
                        TranslateQuery(mc.Arguments[0]);
                        return Single(2, true);
                    case SoodaLinqMethod.Queryable_SingleOrDefaultFiltered:
                        TranslateQuery(mc.Arguments[0]);
                        Where(mc);
                        return Single(2, true);

                    case SoodaLinqMethod.Queryable_Average:
                        return ExecuteAvg(mc) ?? ThrowEmptyAggregate();
                    case SoodaLinqMethod.Queryable_AverageNullable:
                        return ExecuteAvg(mc);
                    case SoodaLinqMethod.Queryable_Max:
                        return ExecuteScalar(mc, "max") ?? ThrowEmptyAggregate();
                    case SoodaLinqMethod.Queryable_Min:
                        return ExecuteScalar(mc, "min") ?? ThrowEmptyAggregate();
                    case SoodaLinqMethod.Queryable_Sum:
                        return ExecuteScalar(mc, "sum")
                            ?? Activator.CreateInstance(mc.Type); // 0, 0L, 0D or 0M

                    default:
                        break;
                }
            }
            TranslateQuery(expr);
            return GetList();
        }

        public SoqlQueryExpression GetSoqlQuery(Expression expr)
        {
            TranslateQuery(expr);
            return CreateSoqlQuery();
        }
    }
}

#endif