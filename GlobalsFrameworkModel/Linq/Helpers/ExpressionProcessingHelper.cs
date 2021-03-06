﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using GlobalsFramework.Linq.ExpressionProcessing;
using InterSystems.Globals;

namespace GlobalsFramework.Linq.Helpers
{
    internal static class ExpressionProcessingHelper
    {
        private static readonly List<IExpressionProcessor> ExpressionProcessors; 

        static ExpressionProcessingHelper()
        {
            ExpressionProcessors = new List<IExpressionProcessor>
            {
                new BinaryExpressionProcessor(),
                new CallExpressionProcessor(),
                new ConstantExpressionProcessor(),
                new MemberExpressionProcessor(),
                new ParameterExpressionProcessor(),
                new UnaryExpressionProcessor(),
                new ConditionalExpressionProcessor(),
                new InvokeExpressionProcessor(),
                new TypeIsExpressionProcessor(),
                new NewExpressionProcessor(),
                new NewArrayExpressionProcessor(),
                new MemberInitExpressionProcessor(),
                new ListInitExpressionProcessor()
            };
        }

        internal static ProcessingResult ProcessPredicate(Expression predicateExpression, IEnumerable<NodeReference> references, DataContext context)
        {
            var unaryExpression = predicateExpression as UnaryExpression;

            if (unaryExpression == null)
                return ProcessingResult.Unsuccessful;

            var lambdaExpression = unaryExpression.Operand as LambdaExpression;

            if ((lambdaExpression == null) || (lambdaExpression.Parameters.Count > 1))
                return ProcessingResult.Unsuccessful;

            return ProcessPredicateInternal(lambdaExpression.Body, references.ToList(), context);
        }

        internal static ProcessingResult ProcessExpression(Expression expression, List<NodeReference> references, DataContext context)
        {
            foreach (var expressionProcessor in ExpressionProcessors)
            {
                if (expressionProcessor.CanProcess(expression))
                    return expressionProcessor.ProcessExpression(expression, references, context);
            }

            return ProcessingResult.Unsuccessful;
        }

        internal static ProcessingResult CopyInstances(ProcessingResult instanceResult, int count, Func<object> processFunc)
        {
            var resultList = new List<object>();

            if (count > 0)
                resultList.Add(instanceResult.Result);

            for (var i = 0; i < count - 1; i++)
                resultList.Add(processFunc());

            return new ProcessingResult(true, resultList);
        }

        private static ProcessingResult ProcessPredicateInternal(Expression predicateExpression, List<NodeReference> references, DataContext context)
        {
            var resultReferences = new List<NodeReference>();

            var processingResult = ProcessExpression(predicateExpression, references, context);
            if (!processingResult.IsSuccess)
                return ProcessingResult.Unsuccessful;

            if (processingResult.IsSingleItem)
            {
                var boolValue = processingResult.GetLoadedItem(predicateExpression.Type) as bool?;

                if (!boolValue.HasValue)
                    return ProcessingResult.Unsuccessful;

                if (boolValue.Value)
                    resultReferences = references;
                return new ProcessingResult(true, resultReferences);
            }

            var resultList = processingResult.GetLoadedItems(predicateExpression.Type);

            if (resultList == null)
                return ProcessingResult.Unsuccessful;

            var index = 0;

            foreach (var item in resultList)
            {
                var boolItemValue = item as bool?;

                if (!boolItemValue.HasValue)
                    return ProcessingResult.Unsuccessful;

                if (boolItemValue.Value)
                    resultReferences.Add(references[index]);

                index++;
            }

            return new ProcessingResult(true, resultReferences);
        }
    }
}
