﻿using NUnit.Framework;
using System.IO;

namespace CoverageBSL.Tests
{
    static class UtilsTest
    {
        public static string XmlString(string path1, string path2, string path3)
        {
            var testDirectory = TestContext.CurrentContext.TestDirectory;
            var xmlFile = Path.Join(testDirectory, path1, path2, path3);
            var xmlString = File.ReadAllText(xmlFile);

            return xmlString;
        }
    }
}