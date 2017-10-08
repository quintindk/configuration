﻿// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Xml;
using Microsoft.Practices.Unity.Configuration.ConfigurationHelpers;
using Unity.Configuration.Properties;

namespace Microsoft.Practices.Unity.Configuration
{
    /// <summary>
    /// Configuration element representing a constructor configuration.
    /// </summary>
    public class ConstructorElement : InjectionMemberElement
    {
        private const string ParametersPropertyName = "";

        /// <summary>
        /// The parameters of the constructor to call.
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
            get { return "constructor"; }
        }

        /// <summary>
        /// Element name to use to serialize this into XML.
        /// </summary>
        public override string ElementName
        {
            get
            {
                return "constructor";
            }
        }

        /// <summary>
        /// Write the contents of this element to the given <see cref="XmlWriter"/>.
        /// </summary>
        /// <remarks>The caller of this method has already written the start element tag before
        /// calling this method, so deriving classes only need to write the element content, not
        /// the start or end tags.</remarks>
        /// <param name="writer">Writer to send XML content to.</param>
        public override void SerializeContent(XmlWriter writer)
        {
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
            var typeToConstruct = toType;

            var constructorToCall = this.FindConstructorInfo(typeToConstruct);

            this.GuardIsMatchingConstructor(typeToConstruct, constructorToCall);

            return new[] { this.MakeInjectionMember(container, constructorToCall) };
        }

        private ConstructorInfo FindConstructorInfo(Type typeToConstruct)
        {
            return typeToConstruct.GetConstructors().Where(this.ConstructorMatches).FirstOrDefault();
        }

        private bool ConstructorMatches(ConstructorInfo candiateConstructor)
        {
            var constructorParams = candiateConstructor.GetParameters();

            if (constructorParams.Length != this.Parameters.Count)
            {
                return false;
            }

            return Parameters.Zip(constructorParams, (a, b) => new Tuple<ParameterElement, ParameterInfo>(a, b))
                             .All(pair => pair.Item1.Matches(pair.Item2));
        }

        private InjectionMember MakeInjectionMember(IUnityContainer container, ConstructorInfo constructorToCall)
        {
            var values = new List<InjectionParameterValue>();
            var parameterInfos = constructorToCall.GetParameters();

            for (int i = 0; i < parameterInfos.Length; ++i)
            {
                values.Add(this.Parameters[i].GetParameterValue(container, parameterInfos[i].ParameterType));
            }

            return new InjectionConstructor(values.ToArray());
        }

        private void GuardIsMatchingConstructor(Type typeToConstruct, ConstructorInfo ctor)
        {
            if (ctor == null)
            {
                string parameterNames = string.Join(", ", this.Parameters.Select(p => p.Name).ToArray());

                throw new InvalidOperationException(
                    string.Format(CultureInfo.CurrentCulture, Resources.NoMatchingConstructor,
                        typeToConstruct.FullName, parameterNames));
            }
        }
    }
}
