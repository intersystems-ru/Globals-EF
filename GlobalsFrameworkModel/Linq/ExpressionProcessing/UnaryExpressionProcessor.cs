﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using GlobalsFramework.Access;
using GlobalsFramework.Linq.Helpers;
using GlobalsFramework.Utils.TypeConversion;
using InterSystems.Globals;

namespace GlobalsFramework.Linq.ExpressionProcessing
{
    internal sealed class UnaryExpressionProcessor : IExpressionProcessor
    {
        public bool CanProcess(Expression expression)
        {
            switch (expression.NodeType)
            {
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                case ExpressionType.ArrayLength:
                case ExpressionType.Negate:
                case ExpressionType.NegateChecked:
                case ExpressionType.Not:
                case ExpressionType.TypeAs:
                case ExpressionType.UnaryPlus:
                case ExpressionType.OnesComplement:
                    return IsPublicType(expression as UnaryExpression);
                default:
                    return false;
            }
        }

        public ProcessingResult ProcessExpression(Expression expression, List<NodeReference> references, DataContext context)
        {
            switch (expression.NodeType)
            {
                case ExpressionType.Convert:
                    return ProcessConvertExpression(expression as UnaryExpression, references, context);
                case ExpressionType.ConvertChecked:
                    return ProcessConvertExpression(expression as UnaryExpression, references, context, true);
                case ExpressionType.ArrayLength:
                    return ProcessArrayLengthExpression(expression as UnaryExpression, references, context);
                case ExpressionType.Negate:
                    return ProcessNegateExpression(expression as UnaryExpression, references, context);
                case ExpressionType.NegateChecked:
                    return ProcessNegateExpression(expression as UnaryExpression, references, context, true);
                case ExpressionType.Not:
                    return ProcessNotExpression(expression as UnaryExpression, references, context);
                case ExpressionType.TypeAs:
                    return ProcessTypeAsExpression(expression as UnaryExpression, references, context);
                case ExpressionType.UnaryPlus:
                    return ProcessUnaryPlusExpression(expression as UnaryExpression, references, context);
                case ExpressionType.OnesComplement:
                    return ProcessOnesComplementExpression(expression as UnaryExpression, references, context);
                default:
                    return ProcessingResult.Unsuccessful;
            }
        }

        private static ProcessingResult ProcessArrayLengthExpression(UnaryExpression expression, List<NodeReference> references, DataContext context)
        {
            var parentResult = ExpressionProcessingHelper.ProcessExpression(expression.Operand, references, context);
            if (!parentResult.IsSuccess)
                return ProcessingResult.Unsuccessful;

            if (parentResult.IsSingleItem)
            {
                var deferredResult = parentResult.GetDeferredItem();
                return deferredResult != null
                    ? new ProcessingResult(true, DatabaseManager.GetEnumerableCount(deferredResult), true)
                    : new ProcessingResult(true, ((Array) parentResult.Result).Length, true);
            }

            var deferredItems = parentResult.GetDeferredItems();

            if (deferredItems != null)
            {
                var resultList = deferredItems.Select(DatabaseManager.GetEnumerableCount).ToList();
                return new ProcessingResult(true, resultList);
            }

            var resultItems = parentResult.GetItems();
            var result = new List<object>();

            foreach (var resultItem in resultItems)
            {
                var array = (Array) resultItem;
                result.Add(array.Length);
            }

            return new ProcessingResult(true, result);
        }

        private static ProcessingResult ProcessConvertExpression(UnaryExpression expression, List<NodeReference> references, DataContext context, bool isChecked = false)
        {
            Func<dynamic, dynamic> convertMethod;

            if (!isChecked)
                convertMethod = value => TypeConverter.ConvertUnchecked(value, expression.Type);
            else
                convertMethod = value => TypeConverter.ConvertChecked(value, expression.Type);

            return PerformUnaryOperation(expression, references, context, convertMethod);
        }

        private static ProcessingResult ProcessNegateExpression(UnaryExpression expression, List<NodeReference> references, DataContext context, bool isChecked = false)
        {
            Func<dynamic, dynamic> negateMethod;

            if (!isChecked)
                negateMethod = value => unchecked (-value);
            else
                negateMethod = value => checked (-value);

            return PerformUnaryOperation(expression, references, context, negateMethod);
        }

        private static ProcessingResult ProcessNotExpression(UnaryExpression expression, List<NodeReference> references, DataContext context)
        {
            //ones complement and logical not operations are mapped to the same Not Expression
            if (!IsLogicalNotOperation(expression))
                return ProcessOnesComplementExpression(expression, references, context);

            Func<dynamic, dynamic> notOperatorMethod = value => !value;
            return PerformUnaryOperation(expression, references, context, notOperatorMethod);
        }

        private static ProcessingResult ProcessTypeAsExpression(UnaryExpression expression, List<NodeReference> references, DataContext context)
        {
            Func<dynamic, dynamic> typeAsMethod = value => TypeConverter.TypeAsOperation(value, expression.Type);
            return PerformUnaryOperation(expression, references, context, typeAsMethod);
        }

        private static ProcessingResult ProcessUnaryPlusExpression(UnaryExpression expression, List<NodeReference> references, DataContext context)
        {
            Func<dynamic, dynamic> unaryPlusMethod = value => +value;
            return PerformUnaryOperation(expression, references, context, unaryPlusMethod);
        }

        private static ProcessingResult ProcessOnesComplementExpression(UnaryExpression expression, List<NodeReference> references, DataContext context)
        {
            Func<dynamic, dynamic> onesComplementMethod = value => ~value;
            return PerformUnaryOperation(expression, references, context, onesComplementMethod);
        }

        private static ProcessingResult PerformUnaryOperation(UnaryExpression expression, List<NodeReference> references,
            DataContext context, Func<dynamic, dynamic> operationResolver)
        {
            var operandResult = ExpressionProcessingHelper.ProcessExpression(expression.Operand, references, context);
            if (!operandResult.IsSuccess)
                return ProcessingResult.Unsuccessful;

            if (operandResult.IsSingleItem)
            {
                var value = operandResult.GetLoadedItem(expression.Operand.Type);
                return new ProcessingResult(true, operationResolver(value), true);
            }

            var values = operandResult.GetLoadedItems(expression.Operand.Type);
            var resultList = new List<object>();

            foreach (var value in values)
            {
                resultList.Add(operationResolver(value));
            }

            return new ProcessingResult(true, resultList);
        }

        private static bool IsPublicType(UnaryExpression expression)
        {
            return expression.Operand.Type.IsPublic;
        }

        private static bool IsLogicalNotOperation(UnaryExpression expression)
        {
            //check if there is custom logical not operator in the type
            if (expression.Method != null)
            {
                return expression.Method.Name == "op_LogicalNot";
            }

            //logical not operator is defined for bool only
            return expression.Operand.Type == typeof (bool);
        }
    }
}
