// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ObjectCloud.ORM.DataAccess.Generator
{
    /// <summary>
    /// Generates C# code to work with a database
    /// </summary>
    public class CSharpGenerator
    {
        public CSharpGenerator() { }

        public CSharpGenerator(string filename, string namespaceVal, IEnumerable<ISubGenerator> subGenerators, params string[] defaultUsings)
        {
            Filename = filename;
            Namespace = namespaceVal;
            SubGenerators.AddRange(subGenerators);
            DefaultUsings = defaultUsings;
        }

        /// <summary>
        /// The filename to use
        /// </summary>
        public string Filename
        {
            get { return filename; }
            set { filename = value; }
        }
        private string filename;

        /// <summary>
        /// The namespace for generated code
        /// </summary>
        public string Namespace
        {
            get { return namespaceVal; }
            set 
            {
                if (null == value)
                    throw new NullReferenceException("Namespace");

                namespaceVal = value;
            }
        }
        private string namespaceVal;

        /// <summary>
        /// All of the sub-generators that go into the source code file
        /// </summary>
        public List<ISubGenerator> SubGenerators
        {
            get { return subGenerators; }
            set 
            {
                if (null == value)
                    throw new NullReferenceException("SubGenerators");

                subGenerators = value;
            }
        }
        private List<ISubGenerator> subGenerators = new List<ISubGenerator>();

        /// <summary>
        /// The default usings for generated code
        /// </summary>
        public string[] DefaultUsings
        {
            get { return defaultUsings; }
            set { defaultUsings = value; }
        }
        private string[] defaultUsings;

        /// <summary>
        /// Generates the source code
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> GenerateAsIEnumerable()
        {
            // Do the usings
            List<string> usings = new List<string>(defaultUsings);

            foreach (ISubGenerator subGenerator in SubGenerators)
                foreach (string usingVal in subGenerator.Usings)
                    if (!usings.Contains(usingVal))
                        usings.Add(usingVal);

            usings.Sort();

            foreach (string usingVal in usings)
                yield return string.Format("using {0};\n", usingVal);

            // namespace
            yield return "\n";
            yield return "namespace " + Namespace + "\n";
            yield return "{\n";

            // do the code
            foreach (ISubGenerator subGenerator in SubGenerators)
            {
                foreach (string codeLine in subGenerator.Generate())
                    yield return codeLine;

                yield return "\n";
            }

            // close namespace
            yield return "}";
        }

        /// <summary>
        /// Generates the source code
        /// </summary>
        /// <returns></returns>
        public string Generate()
        {
            StringBuilder toReturn = new StringBuilder();

            foreach (string s in GenerateAsIEnumerable())
                toReturn.Append(s);

            return toReturn.ToString();
        }

        /// <summary>
        /// Generates source code and writes it to the given file
        /// </summary>
        public void GenerateToFile()
        {
            string generated = Generate();

            // Don't overwrite the same contents
            if (File.Exists(filename))
            {
                string existing = File.ReadAllText(filename);
                
                if (generated.Equals(existing))
                    return;
            }

            File.WriteAllText(filename, generated);
        }
    }
}
