using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using NUnit.Framework;



namespace StringFormatEx.Tests
{
    [TestFixture]
    public class BraceEscapingTests
    {

        [Test]
        public void Escaping1()
        {
            var formatString = @" \{{0}\} \{1} \{\{{2:00\}00}-\} ";
            var args = new object[] { "A", "B", 5555 };
            var expectedOutput = " {A} {1} {{55}55-} ";


            string actualOutput = ExtendedStringFormatter.Default.FormatEx(formatString, args);
            Assert.AreEqual(expectedOutput, actualOutput);
        }


    }
}