// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace ObjectCloud.Common
{
	/// <summary>
	/// Assists in loading a type when the specific type is environment dependant
	/// </summary>
	public static class TypeFunctions
	{
		/// <summary>
		/// Returns the type corresponding with the first given full name
		/// </summary>
		/// <param name="typeNames">
		/// A <see cref="IEnumerable"/> of full type names.  Each type is attempted to be loaded, and the first one found is returned.
		/// </param>
		/// <returns>
		/// The type corresponding with the first given full name
		/// </returns>
		/// <exception cref="TypeLoadException">Thrown if none of the types can be loaded</exception>
		public static Type LoadType(params string[] typeNames)
		{
			return LoadType(typeNames as IEnumerable<string>);
		}
		
		/// <summary>
		/// Returns the type corresponding with the first given full name.  Types can either be in full type, assembly, version, key format, or Spring-style with just full type and assembly format.
		/// </summary>
		/// <param name="typeNames">
		/// A <see cref="IEnumerable"/> of full type names.  Each type is attempted to be loaded, and the first one found is returned.
		/// </param>
		/// <returns>
		/// The type corresponding with the first given full name
		/// </returns>
		/// <exception cref="TypeLoadException">Thrown if none of the types can be loaded</exception>
		public static Type LoadType(IEnumerable<string> typeNames)
		{
			// You shouldn't use an IEnumerable twice incase its yielded...
			List<string> typeNamesDupe = new List<string>();
		
			foreach (string fullTypeName in typeNames)
			{
                Type toReturn = null;
                string[] splitTypeName = fullTypeName.Split(',');

                if (2 == splitTypeName.Length)
                {
                    string typeName = splitTypeName[0].Trim();
                    AssemblyName assemblyName = new AssemblyName(splitTypeName[1].Trim());

                    try
                    {
                        Assembly assembly = Assembly.Load(assemblyName);
                        toReturn = assembly.GetType(typeName, false);
                    }
                    // The FileNotFoundException indicates that the assembly isn't present, so we will move on to the next one
                    catch (FileNotFoundException) { }
                }
                else
                    toReturn = Type.GetType(fullTypeName);

                if (null != toReturn)
                    return toReturn;
                else
				    typeNamesDupe.Add(fullTypeName);
			}
			
			throw new TypeLoadException("None of the following types could be loaded: " + StringGenerator.GenerateCommaSeperatedList(typeNamesDupe));
		}
	}
}
