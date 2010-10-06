using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;



namespace StringFormatEx.Tests
{
    [TestFixture]
    public class BasicFormattingTests
    {
        [Test]
        public void Format1()
        {
            var p = new Person() {
                                     FirstName = "Quentin",
                                     LastName = "Starin",
                                     Age = 29,
                                     Friends = new List<Person>() {}
                                 };
            var formatArgs = new object[] {p.FirstName, p.Age, p.Friends.Count};

            var formatString = "{0} is {1} years old and has {2:N2} friends.";
            var expectedOutput = "Quentin is 29 years old and has 0 friends.";

            var actualOutput = _CustomFormat.CustomFormat(formatString, formatArgs);
            Assert.AreEqual(expectedOutput, actualOutput);
        }
    }
}
