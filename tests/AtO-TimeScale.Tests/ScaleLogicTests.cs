using System;
using System.Globalization;
using Xunit;

namespace AtOTimeScale.Tests
{
    public class ScaleLogicTests
    {
        private static readonly double[] Defaults = { 1, 1.5, 2, 3, 4, 5, 8 };

        private static void WithCulture(string name, Action action)
        {
            var saved = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo(name);
                action();
            }
            finally
            {
                CultureInfo.CurrentCulture = saved;
            }
        }

        [Fact]
        public void Constants_HaveExpectedValues()
        {
            Assert.Equal(0.25, ScaleLogic.MinScale);
            Assert.Equal(20, ScaleLogic.MaxScale);
            Assert.Equal("1, 1.5, 2, 3, 4, 5, 8", ScaleLogic.DefaultSteps);
        }

        [Fact]
        public void ParseSteps_DefaultString()
        {
            var steps = ScaleLogic.ParseSteps(ScaleLogic.DefaultSteps, out var usedDefault);
            Assert.Equal(Defaults, steps);
            Assert.False(usedDefault);
        }

        [Fact]
        public void ParseSteps_WhitespaceAndEmptyEntries()
        {
            var steps = ScaleLogic.ParseSteps(" 2 ,, 1 , ", out var usedDefault);
            Assert.Equal(new double[] { 1, 2 }, steps);
            Assert.False(usedDefault);
        }

        [Fact]
        public void ParseSteps_SkipsGarbageAndNonFinite()
        {
            var steps = ScaleLogic.ParseSteps("abc, 2, NaN, Infinity, 1e999", out var usedDefault);
            Assert.Equal(new double[] { 2 }, steps);
            Assert.False(usedDefault);
        }

        [Fact]
        public void ParseSteps_RemovesDuplicatesWithinTolerance()
        {
            var steps = ScaleLogic.ParseSteps("2, 2.0, 2.00001", out var usedDefault);
            Assert.Equal(new double[] { 2 }, steps);
            Assert.False(usedDefault);
        }

        [Fact]
        public void ParseSteps_ClampsOutOfRangeAndDeduplicates()
        {
            var steps = ScaleLogic.ParseSteps("0, 0.1, 50", out var usedDefault);
            Assert.Equal(new[] { 0.25, 20 }, steps);
            Assert.False(usedDefault);
        }

        [Fact]
        public void ParseSteps_SortsUnsortedInput()
        {
            var steps = ScaleLogic.ParseSteps("8, 0.5, 3, 1", out var usedDefault);
            Assert.Equal(new[] { 0.5, 1, 3, 8 }, steps);
            Assert.False(usedDefault);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(" , ,")]
        [InlineData("abc, NaN, -Infinity, x2")]
        public void ParseSteps_FallsBackToDefault(string text)
        {
            var steps = ScaleLogic.ParseSteps(text, out var usedDefault);
            Assert.Equal(Defaults, steps);
            Assert.True(usedDefault);
        }

        [Fact]
        public void ParseSteps_IgnoresCommaDecimalCulture()
        {
            WithCulture("de-DE", () =>
            {
                var steps = ScaleLogic.ParseSteps("1.5, 2.25", out var usedDefault);
                Assert.Equal(new[] { 1.5, 2.25 }, steps);
                Assert.False(usedDefault);
            });
        }

        [Fact]
        public void Next_AtLastStep_IsUnchanged()
        {
            Assert.Equal(8, ScaleLogic.Next(Defaults, 8));
        }

        [Fact]
        public void Previous_AtFirstStep_IsUnchanged()
        {
            Assert.Equal(1, ScaleLogic.Previous(Defaults, 1));
        }

        [Fact]
        public void Next_StepsThroughWholeList()
        {
            double current = 1;
            for (int i = 1; i < Defaults.Length; i++)
            {
                current = ScaleLogic.Next(Defaults, current);
                Assert.Equal(Defaults[i], current);
            }
            Assert.Equal(8, ScaleLogic.Next(Defaults, current));
        }

        [Fact]
        public void Previous_StepsThroughWholeList()
        {
            double current = 8;
            for (int i = Defaults.Length - 2; i >= 0; i--)
            {
                current = ScaleLogic.Previous(Defaults, current);
                Assert.Equal(Defaults[i], current);
            }
            Assert.Equal(1, ScaleLogic.Previous(Defaults, current));
        }

        [Fact]
        public void NextAndPrevious_FromValueBetweenSteps()
        {
            Assert.Equal(2, ScaleLogic.Next(Defaults, 1.7));
            Assert.Equal(1.5, ScaleLogic.Previous(Defaults, 1.7));
        }

        [Fact]
        public void NextAndPrevious_BelowFirstStep()
        {
            Assert.Equal(1, ScaleLogic.Next(Defaults, 0.5));
            Assert.Equal(0.5, ScaleLogic.Previous(Defaults, 0.5));
        }

        [Fact]
        public void NextAndPrevious_AboveLastStep()
        {
            Assert.Equal(10, ScaleLogic.Next(Defaults, 10));
            Assert.Equal(8, ScaleLogic.Previous(Defaults, 10));
        }

        [Fact]
        public void NextAndPrevious_AbsorbFloatRoundTrip()
        {
            var steps = new[] { 1, 1.1, 1.5 };
            double current = (double)1.1f;
            Assert.NotEqual(1.1, current);
            Assert.Equal(1.5, ScaleLogic.Next(steps, current));
            Assert.Equal(1, ScaleLogic.Previous(steps, current));
        }

        [Theory]
        [InlineData(0.1, 0.25)]
        [InlineData(-3, 0.25)]
        [InlineData(100, 20)]
        [InlineData(2.5, 2.5)]
        [InlineData(double.NaN, 1)]
        [InlineData(double.PositiveInfinity, 1)]
        [InlineData(double.NegativeInfinity, 1)]
        public void Clamp_ClampsAndHandlesNonFinite(double input, double expected)
        {
            Assert.Equal(expected, ScaleLogic.Clamp(input));
        }

        [Theory]
        [InlineData(1.0, true)]
        [InlineData(1.00005, true)]
        [InlineData(1.001, false)]
        [InlineData(2.0, false)]
        public void IsNormal_UsesTolerance(double scale, bool expected)
        {
            Assert.Equal(expected, ScaleLogic.IsNormal(scale));
        }

        [Theory]
        [InlineData(1.5, "x1.5")]
        [InlineData(2.0, "x2.0")]
        [InlineData(0.5, "x0.5")]
        [InlineData(0.25, "x0.3")]
        public void Format_UsesOneDecimal(double scale, string expected)
        {
            Assert.Equal(expected, ScaleLogic.Format(scale));
        }

        [Fact]
        public void Format_IgnoresCommaDecimalCulture()
        {
            WithCulture("de-DE", () => Assert.Equal("x2.5", ScaleLogic.Format(2.5)));
        }
    }
}
