﻿using System;
using NUnit.Framework;
using Statsify.Core.Components.Impl;
using Statsify.Core.Expressions;
using Environment = Statsify.Core.Expressions.Environment;

namespace Statsify.Tests.Core.Expressions
{
    [TestFixture]
    public class ExpressionParserTests
    {
        [Test]
        public void Parse()
        {
            var scanner = new ExpressionScanner();
            var tokens = scanner.Scan("alias_by_fragment(ema(servers.*.system.processor.total*, 50), 2, 4)");

            var parser = new ExpressionParser();
            var expression = parser.Parse(new TokenStream(tokens));

            Environment.RegisterFunction("abs", new Function(typeof(Functions).GetMethod("Abs")));
            Environment.RegisterFunction("ema", new Function(typeof(Functions).GetMethod("Ema")));
            Environment.RegisterFunction("alias_by_fragment", new Function(typeof(Functions).GetMethod("AliasByFragment")));

            var metricManager = new MetricManager(@"c:\statsify");

            var env = new Environment { MetricReader = metricManager, MetricNameResolver = metricManager };
            var r = expression.Evaluate(env, new EvalContext(DateTime.UtcNow.AddMinutes(-20), DateTime.UtcNow));
        }
    }
}