﻿// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml;
using Microsoft.Practices.Unity.Configuration.ConfigurationHelpers;
using Unity.Configuration.Properties;

namespace Microsoft.Practices.Unity.Configuration
{
    /// <summary>
    /// A configuration element representing a method to call.
    /// </summary>
    public class MethodElement : InjectionMemberElement
    {
        private const string NamePropertyName = "name";
        private const string ParametersPropertyName = "";

        // Counter so we have unique keys - need this to handle
        // method injection, can call the same method multiple times.
        private static int methodElementCounter;
        private readonly int methodCount;

        /// <summary>
        /// Construct a new instance of <see cref="MethodElement"/>.
        /// </summary>
        public MethodElement()
        {
            this.methodCount = Interlocked.Increment(ref methodElementCounter);
        }

        /// <summary>
        /// Name of the method to call.
        /// </summary>
        [ConfigurationProperty(NamePropertyName, IsRequired = true)]
        public string Name
        {
            get { return (string)base[NamePropertyName]; }
            set { base[NamePropertyName] = value; }
        }

        /// <summary>
        /// Parameters to the method call.
        /// </summary>
        [ConfigurationProperty(ParametersPropertyName, IsDefaultCollection = true)]
        public ParameterElementCollection Parameters
        {
            get { return (ParameterElementCollection)base[ParametersPropertyName]; }
        }

        /// <summary>
        /// Each element must have a unique key, which is generated by the subclasses.
        /// </summary>
        public override string Key
        {
            get { return string.Format(CultureInfo.InvariantCulture, "method:{0}:{1}", this.Name, this.methodCount); }
        }

        /// <summary>
        /// Element name to use to serialize this into XML.
        /// </summary>
        public override string ElementName
        {
            get { return "method"; }
        }

        /// <summary>
        /// Write the contents of this element to the given <see cref="XmlWriter"/>.
        /// </summary>
        /// <remarks>The caller of this method has already written the start element tag before
        /// calling this method, so deriving classes only need to write the element content, not
        /// the start or end tags.</remarks>
        /// <param name="writer">Writer to send XML content to.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:ValidateArgumentsOfPublicMethods",
            Justification = "Validation done by Guard class")]
        public override void SerializeContent(XmlWriter writer)
        {
            Microsoft.Practices.Unity.Utility.Guard.ArgumentNotNull(writer, "writer");

            writer.WriteAttributeString(NamePropertyName, this.Name);
            foreach (var param in this.Parameters)
            {
                writer.WriteElement("param", param.SerializeContent);
            }
        }

        /// <summary>
        /// Return the set of <see cref="InjectionMember"/>s that are needed
        /// to configure the container according to this configuration element.
        /// </summary>
        /// <param name="container">Container that is being configured.</param>
        /// <param name="fromType">Type that is being registered.</param>
        /// <param name="toType">Type that <paramref name="fromType"/> is being mapped to.</param>
        /// <param name="name">Name this registration is under.</param>
        /// <returns>One or more <see cref="InjectionMember"/> objects that should be
        /// applied to the container registration.</returns>
        public override IEnumerable<InjectionMember> GetInjectionMembers(IUnityContainer container, Type fromType, Type toType, string name)
        {
            MethodInfo methodToCall = this.FindMethodInfo(toType);

            this.GuardIsMatchingMethod(toType, methodToCall);

            return new[] { this.MakeInjectionMember(container, methodToCall) };
        }

        private MethodInfo FindMethodInfo(Type typeToInitialize)
        {
            return typeToInitialize.GetMethods().Where(this.MethodMatches).FirstOrDefault();
        }

        private InjectionMember MakeInjectionMember(IUnityContainer container, MethodInfo methodToCall)
        {
            var parameterValues = new List<InjectionParameterValue>();
            var parameterInfos = methodToCall.GetParameters();

            for (int index = 0; index < parameterInfos.Length; ++index)
            {
                parameterValues.Add(this.Parameters[index].GetParameterValue(container, parameterInfos[index].ParameterType));
            }

            return new InjectionMethod(this.Name, parameterValues.ToArray());
        }

        private bool MethodMatches(MethodInfo method)
        {
            if (method.Name != this.Name)
            {
                return false;
            }

            var methodParameters = method.GetParameters();

            if (methodParameters.Length != this.Parameters.Count)
            {
                return false;
            }

            return Parameters.Zip(methodParameters, (a, b) => new Tuple<ParameterElement, ParameterInfo>(a, b))
                             .All(pair => pair.Item1.Matches(pair.Item2));
        }

        private void GuardIsMatchingMethod(Type typeToInitialize, MethodInfo methodToCall)
        {
            if (methodToCall == null)
            {
                string parameterNames = string.Join(", ", this.Parameters.Select(p => p.Name).ToArray());

                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                    Resources.NoMatchingMethod, typeToInitialize, this.Name, parameterNames));
            }
        }
    }
}
