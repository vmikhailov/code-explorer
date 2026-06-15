using NUnit.Framework;
using CodeExplorer.Core.Parser;

namespace CodeExplorer.Tests
{
    [TestFixture]
    public class SequenceDetectorTests
    {
        [Test]
        public void Test_SequenceDetector_SuffixStrategy()
        {
            var detector = new SequenceDetector<string>();
            var triggerCount = 0;
            IReadOnlyList<string>? matchedPath = null;

            detector.Register(
                new[] { "class", "method", "call" },
                path =>
                {
                    triggerCount++;
                    matchedPath = path;
                },
                SequenceMatchStrategy.Suffix
            );

            // 1. Incomplete sequence
            detector.Push("class");
            detector.Push("method");
            Assert.That(triggerCount, Is.EqualTo(0));

            // 2. Full suffix match
            detector.Push("call");
            Assert.That(triggerCount, Is.EqualTo(1));
            Assert.That(matchedPath, Is.Not.Null);
            Assert.That(matchedPath.Count, Is.EqualTo(3));
            Assert.That(matchedPath[2], Is.EqualTo("call"));

            // 3. Push another element (no longer a match at suffix)
            detector.Push("expression");
            Assert.That(triggerCount, Is.EqualTo(1));

            // 4. Pop back to match
            detector.Pop(); // path was ["class", "method", "call", "expression"], becomes ["class", "method", "call"]
            detector.Pop(); // becomes ["class", "method"]
            detector.Push("call"); // becomes ["class", "method", "call"] -> matches!
            Assert.That(triggerCount, Is.EqualTo(2));
        }

        [Test]
        public void Test_SequenceDetector_ExactStrategy()
        {
            var detector = new SequenceDetector<string>();
            var triggerCount = 0;

            detector.Register(
                new[] { "class", "method" },
                () => triggerCount++,
                SequenceMatchStrategy.Exact
            );

            // Push class
            detector.Push("class");
            Assert.That(triggerCount, Is.EqualTo(0));

            // Push method -> exact match
            detector.Push("method");
            Assert.That(triggerCount, Is.EqualTo(1));

            // Push block -> no longer exact match (length mismatch)
            detector.Push("block");
            Assert.That(triggerCount, Is.EqualTo(1));
        }

        [Test]
        public void Test_SequenceDetector_SubsequenceStrategy()
        {
            var detector = new SequenceDetector<string>();
            var triggerCount = 0;

            detector.Register(
                new[] { "class", "call" },
                () => triggerCount++,
                SequenceMatchStrategy.Subsequence
            );

            detector.Push("class");
            Assert.That(triggerCount, Is.EqualTo(0));

            detector.Push("method");
            Assert.That(triggerCount, Is.EqualTo(0));

            detector.Push("block");
            Assert.That(triggerCount, Is.EqualTo(0));

            // Push call -> subsequence match is triggered since "class" and then "call" are in the path
            detector.Push("call");
            Assert.That(triggerCount, Is.EqualTo(1));
        }

        [Test]
        public void Test_SequenceDetector_Predicates()
        {
            var detector = new SequenceDetector<int>();
            var triggerCount = 0;

            detector.Register(
                new Predicate<int>[] { x => x % 2 == 0, x => x % 2 != 0 },
                () => triggerCount++,
                SequenceMatchStrategy.Suffix
            );

            detector.Push(2); // even
            detector.Push(4); // even -> suffix is even, even. Not a match.
            Assert.That(triggerCount, Is.EqualTo(0));

            detector.Push(5); // odd -> suffix is even, odd. Matches!
            Assert.That(triggerCount, Is.EqualTo(1));
        }
    }
}
