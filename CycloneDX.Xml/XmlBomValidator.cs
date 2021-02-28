// This file is part of the CycloneDX Tool for .NET
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// Copyright (c) Steve Springett. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;

using CycloneDX;
using CycloneDX.Models;

namespace CycloneDX.Xml
{
    public static class XmlBomValidator
    {
        public static async Task<ValidationResult> Validate(string sbom, SchemaVersion schemaVersion)
        {
            var validationMessages = new List<string>();

            var schemaVersionString = schemaVersion.ToString().Substring(1).Replace('_', '.');
            var expectedNamespaceURI = $"http://cyclonedx.org/schema/bom/{schemaVersionString}";

            var assembly = typeof(XmlBomValidator).GetTypeInfo().Assembly;
            using (var schemaStream = assembly.GetManifestResourceStream($"CycloneDX.Xml.Schemas.bom-{schemaVersionString}.xsd"))
            using (var spdxStream = assembly.GetManifestResourceStream("CycloneDX.Xml.Schemas.spdx.xsd"))
            using (var stream = new MemoryStream())
            using (var writer = new StreamWriter(stream))
            {
                var settings = new XmlReaderSettings();

                settings.Schemas.Add(XmlSchema.Read(schemaStream, null));
                settings.Schemas.Add(XmlSchema.Read(spdxStream, null));
                settings.ValidationType = ValidationType.Schema;
            
                await writer.WriteAsync(sbom);
                await writer.FlushAsync();
                stream.Position = 0;

                using (var reader = XmlReader.Create(stream, settings))
                {
                    var document = new XmlDocument();

                    try
                    {
                        document.Load(reader);

                        if (document.DocumentElement.NamespaceURI != expectedNamespaceURI)
                        {
                            validationMessages.Add($"Invalid namespace URI: expected {expectedNamespaceURI} actual {document.DocumentElement.NamespaceURI}");
                        }
                    }
                    catch (XmlSchemaValidationException exc)
                    {
                        var lineInfo = ((IXmlLineInfo)reader);
                        if (lineInfo.HasLineInfo()) {
                            validationMessages.Add($"Validation failed at line number {lineInfo.LineNumber} and position {lineInfo.LinePosition}: {exc.Message}");
                        }
                        else
                        {
                            validationMessages.Add($"Validation failed at position {stream.Position}: {exc.Message}");
                        }
                    }
                }
            }

            return new ValidationResult
            {
                Valid = validationMessages.Count == 0,
                Messages = validationMessages
            };
        }
    }
}
