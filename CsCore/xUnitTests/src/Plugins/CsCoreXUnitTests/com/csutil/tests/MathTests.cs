using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using com.csutil.math;
using Xunit;

namespace com.csutil.tests {

    public class MathTests {

        public MathTests(Xunit.Abstractions.ITestOutputHelper logger) { logger.UseAsLoggingOutput(); }

        [Fact]
        public void ExampleUsage1() {

            List<float> someNumbers = new List<float>();
            float average = 0;
            Assert.True(float.IsNaN(someNumbers.CalcMedian()));
            Assert.True(float.IsNaN(someNumbers.CalcMean()));

            average = AddValue(someNumbers, 4, average);
            Assert.Equal(4f, average);
            Assert.Equal(4f, someNumbers.CalcMedian());
            average = AddValue(someNumbers, 6, average);
            Assert.Equal(5f, average);
            Assert.Equal(4f, someNumbers.CalcMedian());
            average = AddValue(someNumbers, 8, average);
            Assert.Equal(6f, average);
            Assert.Equal(6f, someNumbers.CalcMedian());

        }

        private static float AddValue(List<float> self, float newValue, float oldAverage) {
            var newAverage = CalculateAverage.CalcRunningMean(oldAverage, newValue, self.Count);
            self.Add(newValue);
            return newAverage;
        }

        [Fact]
        public void TestMathEvaluator() {
            Assert.Equal(9, Numbers.Calculate("1 + 2 * 4"));
            Assert.Equal(1, Numbers.Calculate("(1 + 2 * 4) % 2"));
            Assert.Equal(4.5, Numbers.Calculate("(1 + 2 * 4) / 2"));
            Assert.Equal(1, Numbers.Calculate("0.5 + 0.5"));
            Assert.Equal(1, Numbers.Calculate("0.5 + 0,5"));
            Assert.Equal(1, Numbers.Calculate("0,5 + 0,5"));
            Assert.Equal(0.6667, Numbers.Calculate("2/3"), 4);
            Assert.NotEqual(0.667, Numbers.Calculate("2/3"), 4);
        }

        [Fact]
        public static void TestDataTable() {
            var dt = new System.Data.DataTable();
            dt.Columns.Add("A", typeof(int));
            dt.Columns.Add("B", typeof(int));
            dt.Rows.Add(11, 12); // Insert a row with A=4, B=1
            // Querying the table for specific entries:
            var boolResult = dt.Select("A>B-2").Length > 0;
            Assert.True(boolResult); // 11 > 12-2
            // Add a result column that calculates a formula based on the entries:
            var columnName = "Result Column";
            dt.Columns.Add(columnName, typeof(int), "A+B*2");
            var rowNr = 0;
            var valResult = dt.Rows[rowNr][columnName];
            Assert.Equal(35, valResult); // 11 + 12*2  = 35
        }

        [Fact]
        public static void TestColorMath() {

            AssertEqualRgb("[1, 0, 0]", ColorMath.RgbToHsv(1, 0, 0));
            AssertEqualRgb("[0, 1, 0]", ColorMath.RgbToHsv(0, 1, 0));
            AssertEqualRgb("[0, 0, 1]", ColorMath.RgbToHsv(0, 0, 1));

            Assert.Equal(0, ColorMath.CalcBrightness(0, 0, 0));
            Assert.Equal(0.9278, ColorMath.CalcBrightness(1, 1, 0)); // Yellow is bright
            Assert.Equal(1, ColorMath.CalcBrightness(1, 1, 1));

            var whiteBrightnes = ColorMath.CalcBrightness(1, 1, 1);
            var blueBrightness = ColorMath.CalcBrightness(0, 0, 1);
            Assert.Equal(1, whiteBrightnes);
            Assert.Equal(0.0722, blueBrightness); // Blue is dark
            // Blue on white has a great contrast (is well readable):
            Assert.True(8 < ColorMath.CalcContrastRatio(whiteBrightnes, blueBrightness));

            var green = ColorMath.RgbToHsv(0, 1, 0);
            Assert.Equal("[0,3333333, 1, 1]", green.ToStringV2(c => "" + c));
            ColorMath.InvertHue(green); // Green becomes purple
            Assert.Equal("[0,8333333, 1, 1]", green.ToStringV2(c => "" + c));
            AssertEqualRgb("[1, 0, 1]", green); // Purple is red + blue


        }

        private static void AssertEqualRgb(string rgb, float[] hsvColor) {
            var redRgb = ColorMath.HsvToRgb(hsvColor[0], hsvColor[1], hsvColor[2]);
            Assert.Equal(rgb, redRgb.ToStringV2(c => "" + c));
        }

        private const float degreeToRad = MathF.PI / 180f;
        private const float radToDegree = 1 / degreeToRad;

        [Fact]
        public static void VectorRotationTest1() {

            { // Test rotation in circles
                var v1 = new Vector3(2, 0, 0);
                var v2 = v1.Rotate(Quaternion.CreateFromAxisAngle(Vector3.UnitY, 90 * degreeToRad)).Round(4);
                Assert.Equal(new Vector3(0, 0, -2), v2);
                Assert.Equal(90, v2.AngleInDegreeTo(v1));
                Assert.Equal(90, v1.AngleInDegreeTo(v2));

                Assert.Equal(-90, Math.Round(v2.AngleSignedInRadTo(v1, Vector3.UnitY) * radToDegree, 4));
                Assert.Equal(90, Math.Round(v1.AngleSignedInRadTo(v2, Vector3.UnitY) * radToDegree, 4));

                var v3 = v2.Rotate(Quaternion.CreateFromAxisAngle(Vector3.UnitY, 90 * degreeToRad)).Round(4);
                Assert.Equal(new Vector3(-2, 0, 0), v3);
                Assert.Equal(90, v3.AngleInDegreeTo(v2));
                Assert.Equal(180, v3.AngleInDegreeTo(v1));

                Assert.Equal(180, Math.Round(v3.AngleSignedInRadTo(v1, Vector3.UnitY) * radToDegree, 4));
                Assert.Equal(180, Math.Round(v1.AngleSignedInRadTo(v3, Vector3.UnitY) * radToDegree, 4));

                var v4 = v3.Rotate(Quaternion.CreateFromAxisAngle(Vector3.UnitY, 90 * degreeToRad)).Round(4);
                Assert.Equal(new Vector3(0, 0, 2), v4);
                Assert.Equal(90, v4.AngleInDegreeTo(v3));

                Assert.Equal(90, Math.Round(v4.AngleSignedInRadTo(v1, Vector3.UnitY) * radToDegree, 4));
                Assert.Equal(-90, Math.Round(v1.AngleSignedInRadTo(v4, Vector3.UnitY) * radToDegree, 4));

                var v1_2 = v4.Rotate(Quaternion.CreateFromAxisAngle(Vector3.UnitY, 90 * degreeToRad)).Round(4);
                Assert.Equal(v1, v1_2);
            }

        }

        [Fact]
        public static void TestGetRotationDelta() {
            TestGetRotationDeltaWith(80, 100);
            TestGetRotationDeltaWith(100, 80);
            TestGetRotationDeltaWith(350, 10);
            TestGetRotationDeltaWith(10, 350);
        }

        private static void TestGetRotationDeltaWith(float angle1, float angle2) {
            var q1 = Quaternion.CreateFromAxisAngle(Vector3.UnitY, angle1 * degreeToRad);
            var q2 = Quaternion.CreateFromAxisAngle(Vector3.UnitY, angle2 * degreeToRad);
            var delta = q1.GetRotationDeltaTo(q2);

            // Now rotate a vector by this diff and check if the signed angle between the 2 vectors makes sense:
            var expectedAngle = (angle2 - angle1 + 360d) % 360d;
            var a = Vector3.UnitX;
            var b = a.Rotate(delta);
            var angleInRad = a.AngleSignedInRadTo(b, Vector3.UnitY) * radToDegree;
            angleInRad = (angleInRad + 360d) % 360d;
            Assert.Equal(expectedAngle, Math.Round(angleInRad, 3));
        }

        [Fact]
        public void TestMatrixComposeDecompose() {

            var translation = new Vector3(1, 2, 3);
            var rotation = Quaternion.CreateFromYawPitchRoll(15 * degreeToRad, 45 * degreeToRad, 75 * degreeToRad);
            var scale = new Vector3(7, 6, 5);

            // TODO rename Matrix4x4_TRS to compose:
            var matrix = Matrix4x4Extensions.Compose(translation, rotation, scale);

            var success = matrix.Decompose(out var scale2, out var rotation2, out var translation2);
            Assert.True(success);
            var digits = 6;
            Assert.True(translation.IsSimilarTo(translation2, digits: digits));
            Assert.True(rotation.IsSimilarTo(rotation2, digits: digits));
            Assert.True(scale.IsSimilarTo(scale2, digits: digits));

        }

        public class WeightedMedianTests {

            public WeightedMedianTests(Xunit.Abstractions.ITestOutputHelper logger) { logger.UseAsLoggingOutput(); }

            [Fact]
            public void ExampleUsage1() {
                IEnumerable<Element> elements = new List<Element>() {
                    new Element { Value = 80, Weight = 10 },
                    new Element { Value = 90, Weight = 10 },
                    new Element { Value = 95, Weight = 10 }, // Should be the weighted median
                    new Element { Value = 100, Weight = 10 },
                    new Element { Value = 9999, Weight = 10 }, // Outlier that would impact the mean but not the median
                    new Element { Value = 0, Weight = .1 },
                    new Element { Value = 1, Weight = .1 },
                    new Element { Value = 2, Weight = .1 },
                    new Element { Value = 3, Weight = .1 },
                    new Element { Value = 4, Weight = .1 },
                    new Element { Value = 5, Weight = .1 },
                };
                elements = new Random().ShuffleEntries(elements);
                var weightedMedian = elements.CalcWeightedMedian(x => x.Weight);
                Assert.Equal(95, weightedMedian.Item1.Value);
                Assert.Equal(95, weightedMedian.Item2.Value); // Uneven nr of elems, so both results are the same
                // Calculating the normal nedian will not result in the correct value:
                Assert.Equal(5, elements.CalcMedian(x => x.Value));
            }

            [Fact]
            public void ExampleUsage2() {
                IEnumerable<Element> elements = new List<Element>() {
                    new Element { Value = 90, Weight = 10 },
                    new Element { Value = 100, Weight = 10 },
                };
                elements = new Random().ShuffleEntries(elements);
                var weightedMedian = elements.CalcWeightedMedian(x => x.Weight);
                // Since for an even number of elems both median candidates are returned an average can be caluclated on these:
                Assert.Equal(95, (weightedMedian.Item1.Value + weightedMedian.Item2.Value) / 2);
            }

            private class Element : IComparable {
                public double Value { get; set; }
                public double Weight { get; set; }

                public int CompareTo(object obj) {
                    if (obj is Element other) {
                        if (Value == other.Value) { return 0; }
                        return Value < other.Value ? -1 : 1;
                    }
                    throw new InvalidOperationException("Can only compare to other instances of Element");
                }

            }

        }

    }

}