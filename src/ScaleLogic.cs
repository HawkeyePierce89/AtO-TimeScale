using System;
using System.Collections.Generic;
using System.Globalization;

namespace AtOTimeScale
{
    internal static class ScaleLogic
    {
        public const double MinScale = 0.25;
        public const double MaxScale = 20;
        public const string DefaultSteps = "1, 1.5, 2, 3, 4, 5, 8";

        public const double Tolerance = 1e-4;

        public static double[] ParseSteps(string text, out bool usedDefault)
        {
            var steps = Parse(text);
            usedDefault = steps.Length == 0;
            return usedDefault ? Parse(DefaultSteps) : steps;
        }

        private static double[] Parse(string text)
        {
            var values = new List<double>();
            if (text == null)
                return values.ToArray();

            foreach (var part in text.Split(','))
            {
                var trimmed = part.Trim();
                if (trimmed.Length == 0)
                    continue;
                if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    continue;
                if (double.IsNaN(value) || double.IsInfinity(value))
                    continue;
                values.Add(Math.Max(MinScale, Math.Min(MaxScale, value)));
            }

            values.Sort();
            var result = new List<double>();
            foreach (var value in values)
            {
                if (result.Count == 0 || value - result[result.Count - 1] >= Tolerance)
                    result.Add(value);
            }
            return result.ToArray();
        }

        public static double Clamp(double scale)
        {
            if (double.IsNaN(scale) || double.IsInfinity(scale))
                return 1.0;
            return Math.Max(MinScale, Math.Min(MaxScale, scale));
        }

        public static double Next(double[] steps, double current)
        {
            foreach (var step in steps)
            {
                if (step - current >= Tolerance)
                    return step;
            }
            return current;
        }

        public static double Previous(double[] steps, double current)
        {
            for (int i = steps.Length - 1; i >= 0; i--)
            {
                if (current - steps[i] >= Tolerance)
                    return steps[i];
            }
            return current;
        }

        public static bool IsNormal(double scale) => Math.Abs(scale - 1) < Tolerance;

        public static string Format(double scale) => "x" + scale.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
