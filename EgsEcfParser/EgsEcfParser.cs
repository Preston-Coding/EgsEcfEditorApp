using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using static EgsEcfParser.EcfFormatting;
using static EgsEcfParser.EcfKeyValueItem;

namespace EgsEcfParser
{
    public static class EcfFormatting
    {
        private static List<FormatDefinition> Definitions { get; } = new List<FormatDefinition>();
        private static bool DefinitionsLoaded { get; set; } = false;

        public static void ReloadDefinitions()
        {
            DefinitionsLoaded = false;
            XmlLoading.LoadDefinitions();
        }
        public static ReadOnlyCollection<string> GetSupportedFileTypes()
        {
            XmlLoading.LoadDefinitions();
            return Definitions.Select(def => def.FileType).ToList().AsReadOnly();
        }
        public static bool TryGetDefinition(string filePathAndName, out FormatDefinition definition)
        {
            XmlLoading.LoadDefinitions();
            string fileName = Path.GetFileNameWithoutExtension(filePathAndName);
            definition = Definitions.FirstOrDefault(def => fileName.Contains(def.FileType));
            return definition != null;
        }
        public static FormatDefinition GetDefinition(string fileType)
        {
            XmlLoading.LoadDefinitions();
            FormatDefinition definition = Definitions.FirstOrDefault(def => def.FileType.Equals(fileType));
            if (definition == null) { throw new ArgumentException(string.Format("FileType '{0}' is not supported", fileType)); }
            return definition;
        }
        public static Encoding GetFileEncoding(string filePathAndName)
        {
            try
            {
                using (FileStream stream = new FileStream(filePathAndName, FileMode.Open, FileAccess.Read))
                {
                    byte[] bom = new byte[4];
                    stream.Read(bom, 0, 4);
                    try
                    {
                        if (bom[0] == 0x2b && bom[1] == 0x2f && bom[2] == 0x76) return Encoding.UTF7;
                        if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf) return Encoding.UTF8;
                        if (bom[0] == 0xff && bom[1] == 0xfe && bom[2] == 0 && bom[3] == 0) return Encoding.UTF32; //UTF-32LE
                        if (bom[0] == 0xff && bom[1] == 0xfe) return Encoding.Unicode; //UTF-16LE
                        if (bom[0] == 0xfe && bom[1] == 0xff) return Encoding.BigEndianUnicode; //UTF-16BE
                        if (bom[0] == 0 && bom[1] == 0 && bom[2] == 0xfe && bom[3] == 0xff) return new UTF32Encoding(true, true);  //UTF-32BE
                    }
                    catch (Exception) { }
                    return new UTF8Encoding(false);
                }
            }
            catch (Exception ex)
            {
                throw new IOException(string.Format("File {0} encoding could not be loaded: {1}", filePathAndName, ex.Message));
            }
        }
        public static EcfFileNewLineSymbols GetNewLineSymbol(string filePathAndName)
        {
            try
            {
                StringBuilder charBuffer = new StringBuilder();
                using (StreamReader reader = new StreamReader(File.Open(filePathAndName, FileMode.Open, FileAccess.Read)))
                {
                    do
                    {
                        if (ReadChar(reader, out char buffer) != -1)
                        {
                            if (buffer == '\n')
                            {
                                return EcfFileNewLineSymbols.Lf;
                            }
                            else if (buffer == '\r')
                            {
                                if (ReadChar(reader, out buffer) != -1 && buffer == '\n')
                                {
                                    return EcfFileNewLineSymbols.CrLf;
                                }
                                else
                                {
                                    return EcfFileNewLineSymbols.Cr;
                                }
                            }
                        }
                    } while (!reader.EndOfStream);
                }
                return EcfFileNewLineSymbols.CrLf;
            }
            catch (Exception ex)
            {
                throw new IOException(string.Format("File {0} newline character could not be determined: {1}", filePathAndName, ex.Message));
            }
        }
        public static string GetNewLineChar(EcfFileNewLineSymbols newLineSymbol)
        {
            switch (newLineSymbol)
            {
                case EcfFileNewLineSymbols.Lf: return "\n";
                case EcfFileNewLineSymbols.Cr: return "\r";
                default: return "\r\n";
            }
        }
        public static List<ItemDefinition> FindDeprecatedItemDefinitions(EgsEcfFile file)
        {
            List<ItemDefinition> deprecatedItems = new List<ItemDefinition>();
            try
            {
                FormatDefinition definition = GetDefinition(file.Definition.FileType);
                List<ItemDefinition> rootBlockAttributes = definition.RootBlockAttributes.ToList();
                List<ItemDefinition> childBlockAttributes = definition.ChildBlockAttributes.ToList();
                List<ItemDefinition> blockParameters = definition.BlockParameters.ToList();
                List<ItemDefinition> parameterAttributes = definition.ParameterAttributes.ToList();
                foreach (EcfStructureItem item in file.GetDeepItemList<EcfStructureItem>())
                {
                    RemoveDeprecatedItemDefinitions(item, rootBlockAttributes, childBlockAttributes, blockParameters, parameterAttributes);
                    if (rootBlockAttributes.Count == 0 && childBlockAttributes.Count == 0 && blockParameters.Count == 0 && parameterAttributes.Count == 0)
                    {
                        break;
                    }
                }
                deprecatedItems.AddRange(rootBlockAttributes);
                deprecatedItems.AddRange(childBlockAttributes);
                deprecatedItems.AddRange(blockParameters);
                deprecatedItems.AddRange(parameterAttributes);
            }
            catch (Exception) { }
            return deprecatedItems;
        }
        
        public static bool IsKeyValid(string key)
        {
            return !string.IsNullOrEmpty(key);
        }
        public static List<EcfError> CheckBlockPreMark(string dataType, ReadOnlyCollection<BlockValueDefinition> definedDataTypes)
        {
            return CheckBlockDataType(dataType, definedDataTypes, EcfErrors.BlockPreMarkMissing, EcfErrors.BlockPreMarkUnknown);
        }
        public static List<EcfError> CheckBlockDataType(string dataType, ReadOnlyCollection<BlockValueDefinition> definedDataTypes)
        {
            return CheckBlockDataType(dataType, definedDataTypes, EcfErrors.BlockDataTypeMissing , EcfErrors.BlockDataTypeUnknown);
        }
        public static List<EcfError> CheckBlockPostMark(string dataType, ReadOnlyCollection<BlockValueDefinition> definedDataTypes)
        {
            return CheckBlockDataType(dataType, definedDataTypes, EcfErrors.BlockPostMarkMissing, EcfErrors.BlockPostMarkUnknown);
        }
        public static List<EcfError> CheckBlockDataType(string dataType, ReadOnlyCollection<BlockValueDefinition> definedDataTypes, EcfErrors missingError, EcfErrors unknownError)
        {
            List<EcfError> errors = new List<EcfError>();
            List<BlockValueDefinition> mandatoryDataTypes = definedDataTypes.Where(type => !type.IsOptional).ToList();
            if (mandatoryDataTypes.Count > 0 && !mandatoryDataTypes.Any(type => type.Value.Equals(dataType)))
            {
                errors.Add(new EcfError(missingError, string.Format("found '{0}', expected: '{1}'", dataType,
                    string.Join(", ", mandatoryDataTypes.Select(type => type.Value).ToArray()))));
            }
            else if (dataType != null && !definedDataTypes.Any(type => type.Value.Equals(dataType)))
            {
                errors.Add(new EcfError(unknownError, dataType));
            }
            return errors;
        }
        public static EcfError CheckBlockReferenceValid(EcfBlock block, List<EcfBlock> blockList, out EcfBlock inheriter)
        {
            string reference = block.RefSource;
            if (reference == null) {
                inheriter = null;
                return null; 
            }
            inheriter = blockList.FirstOrDefault(parentBlock => parentBlock.RefTarget?.Equals(reference) ?? false);
            if (inheriter == null)
            {
                return new EcfError(EcfErrors.BlockInheritorMissing, reference);
            }
            return null;
        }
        public static List<ItemDefinition> CheckItemsMissing<T>(List<T> parameters, ReadOnlyCollection<ItemDefinition> definedParameters) where T : EcfKeyValueItem
        {
            return definedParameters?.Where(defParam => !defParam.IsOptional && !parameters.Any(param => param.Key.Equals(defParam.Name))).ToList();
        }
        public static List<T> CheckItemsDoubled<T>(List<T> items) where T : EcfKeyValueItem
        {
            return items.Except(items.Distinct(KeyItemComparer)).Cast<T>().ToList();
        }
        public static EcfError CheckItemUnknown(ReadOnlyCollection<ItemDefinition> definition, string key, KeyValueItemTypes itemType, out ItemDefinition itemDefinition)
        {
            itemDefinition = definition?.FirstOrDefault(defParam => defParam.Name.Equals(key));
            if (itemDefinition != null) { return null; }

            switch (itemType)
            {
                case KeyValueItemTypes.Parameter: return new EcfError(EcfErrors.ParameterUnknown, key);
                case KeyValueItemTypes.Attribute: return new EcfError(EcfErrors.AttributeUnknown, key);
                default: return new EcfError(EcfErrors.Unknown, key);
            }
        }
        public static List<EcfError> CheckParametersValid(List<EcfParameter> parameters, ReadOnlyCollection<ItemDefinition> definedParameters)
        {
            List<EcfError> errors = new List<EcfError>();
            CheckItemsMissing(parameters, definedParameters).ForEach(missingParam =>
            {
                errors.Add(new EcfError(EcfErrors.ParameterMissing, missingParam.Name));
            });
            CheckItemsDoubled(parameters).ForEach(doubledParam =>
            {
                errors.Add(new EcfError(EcfErrors.ParameterDoubled, doubledParam.Key));
            });
            return errors;
        }
        public static List<EcfError> CheckAttributesValid(List<EcfAttribute> attributes, ReadOnlyCollection<ItemDefinition> definedAttributes)
        {
            List<EcfError> errors = new List<EcfError>();
            CheckItemsMissing(attributes, definedAttributes)?.ForEach(missingAttr =>
            {
                errors.Add(new EcfError(EcfErrors.AttributeMissing, missingAttr.Name));
            });
            CheckItemsDoubled(attributes).ForEach(doubledAttr =>
            {
                errors.Add(new EcfError(EcfErrors.AttributeDoubled, doubledAttr.Key));
            });
            return errors;
        }
        public static List<EcfError> CheckValuesValid(List<EcfValueGroup> groups, ItemDefinition itemDef, FormatDefinition formatDef)
        {
            List<EcfError> errors = new List<EcfError>();
            if (groups == null || !groups.Any(group => group.Values.Count > 0))
            {
                if (itemDef?.HasValue ?? true)
                {
                    errors.Add(new EcfError(EcfErrors.ValueGroupEmpty, "Not at least one value present"));
                }
            }
            else
            {
                int groupCount = 1;
                int valueCount = 1;
                foreach (EcfValueGroup group in groups)
                {
                    foreach (string value in group.Values)
                    {
                        errors.AddRange(CheckValueValid(value, itemDef, formatDef, string.Format("group: {0}, value: {1}", groupCount, valueCount)));
                        valueCount++;
                    }
                    groupCount++;
                    valueCount = 1;
                }
            }
            return errors;
        }
        public static List<EcfError> CheckValueValid(string value, ItemDefinition itemDef, FormatDefinition formatDef, string errorInfo)
        {
            List<EcfError> errors = new List<EcfError>();
            if (value == null) 
            {
                errors.Add(new EcfError(EcfErrors.ValueNull, errorInfo ?? "Value null"));
            }
            else if (!(itemDef?.AllowBlank ?? false) && value.Equals(string.Empty))
            {
                errors.Add(new EcfError(EcfErrors.ValueEmpty, errorInfo ?? "Value empty"));
            }
            else
            {
                List<string> foundProhibitedPhrases = formatDef?.ProhibitedValuePhrases.Where(phrase => value.Contains(phrase)).ToList();
                foundProhibitedPhrases?.ForEach(phrase =>
                {
                    errors.Add(new EcfError(EcfErrors.ValueContainsProhibitedPhrases, phrase));
                });
            }
            return errors;
        }

        private static void RemoveDeprecatedItemDefinitions(EcfStructureItem item,
            List<ItemDefinition> rootBlockAttributes, List<ItemDefinition> childBlockAttributes,
            List<ItemDefinition> blockParameters, List<ItemDefinition> parameterAttributes)
        {
            if (item is EcfParameter parameter)
            {
                if (parameterAttributes.Count > 0)
                {
                    parameterAttributes.RemoveAll(defAttr => parameter.Attributes.Any(attr => defAttr.Name.Equals(attr.Key)));
                }
                if (blockParameters.Count > 0)
                {
                    blockParameters.RemoveAll(defParam => defParam.Name.Equals(parameter.Key));
                }
            }
            else if (item is EcfBlock block)
            {
                if (block.IsRoot())
                {
                    if (rootBlockAttributes.Count > 0)
                    {
                        rootBlockAttributes.RemoveAll(defAttr => block.Attributes.Any(attr => defAttr.Name.Equals(attr.Key)));
                    }
                }
                else
                {
                    if (childBlockAttributes.Count > 0)
                    {
                        childBlockAttributes.RemoveAll(defAttr => block.Attributes.Any(attr => defAttr.Name.Equals(attr.Key)));
                    }
                }
            }
        }
        private static int ReadChar(StreamReader reader, out char c)
        {
            int i = reader.Read();
            if (i >= 0)
            {
                c = (char)i;
            }
            else
            {
                c = (char)0;
            }
            return i;
        }
        private static IKeyItemComparer KeyItemComparer { get; } = new IKeyItemComparer();
        private class IKeyItemComparer : IEqualityComparer<EcfKeyValueItem>
        {
            public bool Equals(EcfKeyValueItem item1, EcfKeyValueItem item2)
            {
                if (item1 is null && item2 is null) return true;
                if (item1 is null || item2 is null) return false;
                return item1.Key.Equals(item2.Key);
            }
            public int GetHashCode(EcfKeyValueItem item)
            {
                return item.Key.GetHashCode();
            }
        }

        public enum EcfFileNewLineSymbols
        {
            Unknown,
            Lf,
            Cr,
            CrLf,
        }

        private static class XmlLoading
        {
            private static class XmlSettings
            {
                public static string FolderName { get; } = "EcfFileDefinitions";
                public static string TemplateFileName { get; } = "EcfFileDefinitions_BlocksConfig.xml";
                public static string FileNamePattern { get; } = "*.xml";

                public static string XChapterRoot { get; } = "Settings";

                public static string XChapterFileConfig { get; } = "Config";
                public static string XChapterFormatting { get; } = "Formatting";
                public static string XChapterBlockTypePreMarks { get; } = "BlockTypePreMarks";
                public static string XChapterBlockTypePostMarks { get; } = "BlockTypePostMarks";
                public static string XChapterRootBlockTypes { get; } = "RootBlockTypes";
                public static string XChapterRootBlockAttributes { get; } = "RootBlockAttributes";
                public static string XChapterChildBlockTypes { get; } = "ChildBlockTypes";
                public static string XChapterChildBlockAttributes { get; } = "ChildBlockAttributes";
                public static string XChapterBlockParameters { get; } = "BlockParameters";
                public static string XChapterParameterAttributes { get; } = "ParameterAttributes";

                public static string XElementSingleLineCommentStart { get; } = "SingleLineCommentStart";
                public static string XElementMultiLineCommentPair { get; } = "MultiLineCommentPair";
                public static string XElementBlockIdentifierPair { get; } = "BlockIdentifierPair";
                public static string XElementOuterTrimmingChar { get; } = "OuterTrimmingChar";
                public static string XElementItemSeperator { get; } = "ItemSeperator";
                public static string XElementItemValueSeperator { get; } = "ItemValueSeperator";
                public static string XElementValueSeperator { get; } = "ValueSeperator";
                public static string XElementValueGroupSeperator { get; } = "ValueGroupSeperator";
                public static string XElementValueFractionalSeperator { get; } = "ValueFractionalSeperator";
                public static string XElementMagicSpacer { get; } = "MagicSpacer";
                public static string XElementBlockIdentificationAttribute { get; } = "BlockIdentificationAttribute";
                public static string XElementBlockReferenceSourceAttribute { get; } = "BlockReferenceSourceAttribute";
                public static string XElementBlockReferenceTargetAttribute { get; } = "BlockReferenceTargetAttribute";
                public static string XElementEscapeIdentifierPair { get; } = "EscapeIdentifierPair";
                public static string XElementParamter { get; } = "Param";

                public static string XAttributeType { get; } = "type";
                public static string XAttributeValue { get; } = "value";
                public static string XAttributeOpener { get; } = "opener";
                public static string XAttributeCloser { get; } = "closer";
                public static string XAttributeName { get; } = "name";
                public static string XAttributeOptional { get; } = "optional";
                public static string XAttributeHasValue { get; } = "hasValue";
                public static string XAttributeAllowBlank { get; } = "allowBlank";
                public static string XAttributeForceEscape { get; } = "forceEscape";
                public static string XAttributeInfo { get; } = "info";
            }
            private static XmlDocument XmlDoc { get; } = new XmlDocument();

            public static void LoadDefinitions()
            {
                if (!DefinitionsLoaded)
                {
                    Definitions.Clear();
                    Directory.CreateDirectory(XmlSettings.FolderName);
                    try
                    {
                        foreach (string filePathAndName in Directory.GetFiles(XmlSettings.FolderName, XmlSettings.FileNamePattern))
                        {
                            try
                            {
                                ReadDefinitionFile(filePathAndName);
                            }
                            catch(Exception ex)
                            {
                                throw new IOException(string.Format("Settings file '{0}' could not be loaded: {1}", filePathAndName, ex.Message));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new IOException(string.Format("Settings files could not be found: {0}", ex.Message));
                    }
                    if (Definitions.Count == 0)
                    {
                        string filePathAndName = "";
                        try
                        {
                            filePathAndName = Path.Combine(XmlSettings.FolderName, XmlSettings.TemplateFileName);
                            CreateXmlTemplate(filePathAndName);
                            ReadDefinitionFile(filePathAndName);
                        }
                        catch (Exception ex)
                        {
                            throw new IOException(string.Format("Template settings file '{0}' could not be loaded: {1}", filePathAndName, ex.Message));
                        }
                    }
                    DefinitionsLoaded = true;
                }
            }

            private static void ReadDefinitionFile(string filePathAndName)
            {
                XmlDoc.Load(filePathAndName);
                foreach (XmlNode configNode in XmlDoc.SelectNodes(string.Format("//{0}", XmlSettings.XChapterFileConfig)))
                {
                    string fileType = configNode.Attributes?.GetNamedItem(XmlSettings.XAttributeType)?.Value;
                    if (IsKeyValid(fileType) && Definitions.All(def => !def.FileType.Equals(fileType)))
                    {
                        Definitions.Add(BuildFormatDefinition(filePathAndName, configNode));
                    }
                }
            }
            private static FormatDefinition BuildFormatDefinition(string filePathAndName, XmlNode configNode)
            {
                string fileType = configNode?.Attributes.GetNamedItem(XmlSettings.XAttributeType)?.Value;
                if (fileType == null) { throw new ArgumentException(string.Format("Attribute {0} not found", XmlSettings.XAttributeType)); }

                XmlNode formatterNode = configNode.SelectSingleNode(XmlSettings.XChapterFormatting);
                if (formatterNode == null) { throw new ArgumentException(string.Format("Chapter {0} not found", XmlSettings.XChapterFormatting)); }

                List<string> singleLineCommentStarts = BuildStringList(formatterNode, XmlSettings.XElementSingleLineCommentStart);
                if (singleLineCommentStarts.Count < 1) { throw new ArgumentException(string.Format("No valid {0} found", XmlSettings.XElementSingleLineCommentStart)); }
                List<StringPairDefinition> multiLineCommentPairs = BuildStringPairList(formatterNode, XmlSettings.XElementMultiLineCommentPair);
                List<StringPairDefinition> blockPairs = BuildStringPairList(formatterNode, XmlSettings.XElementBlockIdentifierPair);
                if (blockPairs.Count < 1) { throw new ArgumentException(string.Format("No valid {0} found", XmlSettings.XElementBlockIdentifierPair)); }
                List<StringPairDefinition> escapeIdentifierPairs = BuildStringPairList(formatterNode, XmlSettings.XElementEscapeIdentifierPair);
                if (escapeIdentifierPairs.Count < 1) { throw new ArgumentException(string.Format("No valid {0} found", XmlSettings.XElementEscapeIdentifierPair)); }
                List<string> outerTrimmingPhrases = BuildStringList(formatterNode, XmlSettings.XElementOuterTrimmingChar);

                string itemSeperator = RepairXmlControlLiterals(formatterNode.SelectSingleNode(XmlSettings.XElementItemSeperator)?
                    .Attributes?.GetNamedItem(XmlSettings.XAttributeValue)?.Value);
                string itemValueSeperator = RepairXmlControlLiterals(formatterNode.SelectSingleNode(XmlSettings.XElementItemValueSeperator)?
                    .Attributes?.GetNamedItem(XmlSettings.XAttributeValue)?.Value);
                string valueSeperator = RepairXmlControlLiterals(formatterNode.SelectSingleNode(XmlSettings.XElementValueSeperator)?
                    .Attributes?.GetNamedItem(XmlSettings.XAttributeValue)?.Value);
                string valueGroupSeperator = RepairXmlControlLiterals(formatterNode.SelectSingleNode(XmlSettings.XElementValueGroupSeperator)?
                    .Attributes?.GetNamedItem(XmlSettings.XAttributeValue)?.Value);
                string valueFractionalSeperator = RepairXmlControlLiterals(formatterNode.SelectSingleNode(XmlSettings.XElementValueFractionalSeperator)?
                    .Attributes?.GetNamedItem(XmlSettings.XAttributeValue)?.Value);
                string magicSpacer = RepairXmlControlLiterals(formatterNode.SelectSingleNode(XmlSettings.XElementMagicSpacer)?
                    .Attributes?.GetNamedItem(XmlSettings.XAttributeValue)?.Value);
                string blockIdentificationAttribute = RepairXmlControlLiterals(formatterNode.SelectSingleNode(XmlSettings.XElementBlockIdentificationAttribute)?
                    .Attributes?.GetNamedItem(XmlSettings.XAttributeValue)?.Value);
                string blockReferenceSourceAttribute = RepairXmlControlLiterals(formatterNode.SelectSingleNode(XmlSettings.XElementBlockReferenceSourceAttribute)?
                    .Attributes?.GetNamedItem(XmlSettings.XAttributeValue)?.Value);
                string blockReferenceTargetAttribute = RepairXmlControlLiterals(formatterNode.SelectSingleNode(XmlSettings.XElementBlockReferenceTargetAttribute)?
                    .Attributes?.GetNamedItem(XmlSettings.XAttributeValue)?.Value);
                
                if (!IsKeyValid(itemSeperator)) { throw new ArgumentException(string.Format("Element {0} not valid", XmlSettings.XElementItemSeperator)); }
                if (!IsKeyValid(itemValueSeperator)) { throw new ArgumentException(string.Format("Element {0} not valid", XmlSettings.XElementItemValueSeperator)); }
                if (!IsKeyValid(valueSeperator)) { throw new ArgumentException(string.Format("Element {0} not valid", XmlSettings.XElementValueSeperator)); }
                if (!IsKeyValid(valueGroupSeperator)) { throw new ArgumentException(string.Format("Element {0} not valid", XmlSettings.XElementValueGroupSeperator)); }
                if (!IsKeyValid(valueFractionalSeperator)) { throw new ArgumentException(string.Format("Element {0} not valid", XmlSettings.XElementValueFractionalSeperator)); }
                if (!IsKeyValid(magicSpacer)) { throw new ArgumentException(string.Format("Element {0} not valid", XmlSettings.XElementMagicSpacer)); }

                List<BlockValueDefinition> blockTypePreMarks = BuildMarkList(configNode, XmlSettings.XChapterBlockTypePreMarks);
                List<BlockValueDefinition> blockTypePostMarks = BuildMarkList(configNode, XmlSettings.XChapterBlockTypePostMarks);
                List<BlockValueDefinition> rootBlockTypes = BuildBlockTypeList(configNode, XmlSettings.XChapterRootBlockTypes);
                List<ItemDefinition> rootBlockAttributes = BuildItemList(configNode, XmlSettings.XChapterRootBlockAttributes);
                List<BlockValueDefinition> childBlockTypes = BuildBlockTypeList(configNode, XmlSettings.XChapterChildBlockTypes);
                List<ItemDefinition> childBlockAttributes = BuildItemList(configNode, XmlSettings.XChapterChildBlockAttributes);
                List<ItemDefinition> blockParameters = BuildItemList(configNode, XmlSettings.XChapterBlockParameters);
                List<ItemDefinition> parameterAttributes = BuildItemList(configNode, XmlSettings.XChapterParameterAttributes);

                return new FormatDefinition(filePathAndName, fileType,
                    singleLineCommentStarts, multiLineCommentPairs,
                    blockPairs, escapeIdentifierPairs, outerTrimmingPhrases,
                    itemSeperator, itemValueSeperator, valueSeperator, 
                    valueGroupSeperator, valueFractionalSeperator, magicSpacer,
                    blockIdentificationAttribute, blockReferenceSourceAttribute, blockReferenceTargetAttribute,
                    blockTypePreMarks, blockTypePostMarks,
                    rootBlockTypes, rootBlockAttributes,
                    childBlockTypes, childBlockAttributes,
                    blockParameters, parameterAttributes
                );
            }
            private static string RepairXmlControlLiterals(string xmlString)
            {
                return xmlString?.Replace("\\t", "\t").Replace("\\r", "\r").Replace("\\n", "\n").Replace("\\v", "\v");
            }

            private static void CreateXmlTemplate(string filePathAndName)
            {
                XmlWriterSettings settings = new XmlWriterSettings
                {
                    Indent = true,
                };
                using (XmlWriter writer = XmlWriter.Create(filePathAndName, settings))
                {
                    writer.WriteStartElement(XmlSettings.XChapterRoot);
                    writer.WriteComment("Copy Config struct to define new file types if needed");
                    writer.WriteStartElement(XmlSettings.XChapterFileConfig);
                    writer.WriteAttributeString(XmlSettings.XAttributeType, "BlocksConfig");
                    writer.WriteComment("Ecf Syntax Format Settings");
                    // Formatting
                    {
                        writer.WriteStartElement(XmlSettings.XChapterFormatting);
                        writer.WriteComment("Copy Parameters if more needed");
                        CreateXmlPairValueItem(writer, XmlSettings.XElementMultiLineCommentPair, "/*", "*/");
                        CreateXmlSpecificValueItem(writer, XmlSettings.XElementOuterTrimmingChar, " ");
                        CreateXmlSpecificValueItem(writer, XmlSettings.XElementOuterTrimmingChar, "\\t");
                        CreateXmlSpecificValueItem(writer, XmlSettings.XElementOuterTrimmingChar, "\\v");
                        CreateXmlSpecificValueItem(writer, XmlSettings.XElementOuterTrimmingChar, "\\r");
                        CreateXmlSpecificValueItem(writer, XmlSettings.XElementOuterTrimmingChar, "\\n");
                        writer.WriteComment("Specific Parameters");
                        CreateXmlSpecificValueItem(writer, XmlSettings.XElementItemSeperator, ",");
                        CreateXmlSpecificValueItem(writer, XmlSettings.XElementItemValueSeperator, ":");
                        CreateXmlSpecificValueItem(writer, XmlSettings.XElementValueSeperator, ",");
                        CreateXmlSpecificValueItem(writer, XmlSettings.XElementValueGroupSeperator, ";");
                        CreateXmlSpecificValueItem(writer, XmlSettings.XElementValueFractionalSeperator, ".");
                        CreateXmlSpecificValueItem(writer, XmlSettings.XElementMagicSpacer, " ");
                        CreateXmlSpecificValueItem(writer, XmlSettings.XElementBlockIdentificationAttribute, "Id");
                        CreateXmlSpecificValueItem(writer, XmlSettings.XElementBlockReferenceSourceAttribute, "Ref");
                        CreateXmlSpecificValueItem(writer, XmlSettings.XElementBlockReferenceTargetAttribute, "Name");
                        writer.WriteComment("Copy Parameter if more needed, First is used at file write");
                        CreateXmlSpecificValueItem(writer, XmlSettings.XElementSingleLineCommentStart, "#");
                        CreateXmlPairValueItem(writer, XmlSettings.XElementBlockIdentifierPair, "{", "}");
                        CreateXmlPairValueItem(writer, XmlSettings.XElementEscapeIdentifierPair, "\"", "\"");
                        writer.WriteEndElement();
                    }
                    writer.WriteComment("File Specific Syntax Settings, Add more child-params if needed");
                    // premarks
                    {
                        writer.WriteStartElement(XmlSettings.XChapterBlockTypePreMarks);
                        CreateXmlOptionalValueItem(writer, "+", true);
                        writer.WriteEndElement();
                        writer.WriteStartElement(XmlSettings.XChapterBlockTypePostMarks);
                        CreateXmlOptionalValueItem(writer, " ", false);
                        writer.WriteEndElement();
                    }
                    // Root block types
                    {
                        writer.WriteStartElement(XmlSettings.XChapterRootBlockTypes);
                        CreateXmlTypeItem(writer, "Block", false);
                        writer.WriteEndElement();
                    }
                    // root block Attributes
                    {
                        writer.WriteStartElement(XmlSettings.XChapterRootBlockAttributes);;
                        CreateXmlParameterItem(writer, "Id", true, true, false, false);
                        CreateXmlParameterItem(writer, "Name", false, true, false, false);
                        CreateXmlParameterItem(writer, "Ref", true, true, false, false);
                        writer.WriteEndElement();
                    }
                    // Child block types
                    {
                        writer.WriteStartElement(XmlSettings.XChapterChildBlockTypes);
                        CreateXmlTypeItem(writer, "Child", false);
                        writer.WriteEndElement();
                    }
                    // child block Attributes
                    {
                        writer.WriteStartElement(XmlSettings.XChapterChildBlockAttributes);
                        CreateXmlParameterItem(writer, "DropOnDestroy", true, false, false, false);
                        writer.WriteEndElement();
                    }
                    // block parameters
                    {
                        writer.WriteStartElement(XmlSettings.XChapterBlockParameters);
                        CreateXmlParameterItem(writer, "Material", true, true, false, false);
                        CreateXmlParameterItem(writer, "Shape", true, true, false, false);
                        CreateXmlParameterItem(writer, "Mesh", true, true, false, false);
                        writer.WriteEndElement();
                    }
                    // parameter Attributes
                    {
                        writer.WriteStartElement(XmlSettings.XChapterParameterAttributes);
                        CreateXmlParameterItem(writer, "type", true, true, false, false);
                        CreateXmlParameterItem(writer, "display", true, true, false, false);
                        CreateXmlParameterItem(writer, "formatter", true, true, false, false);
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }
            }
            private static void CreateXmlPairValueItem(XmlWriter writer, string name, string opener, string closer)
            {
                writer.WriteStartElement(name);
                writer.WriteAttributeString(XmlSettings.XAttributeOpener, opener);
                writer.WriteAttributeString(XmlSettings.XAttributeCloser, closer);
                writer.WriteEndElement();
            }
            private static void CreateXmlSpecificValueItem(XmlWriter writer, string name, string value)
            {
                writer.WriteStartElement(name);
                writer.WriteAttributeString(XmlSettings.XAttributeValue, value);
                writer.WriteEndElement();
            }
            private static void CreateXmlOptionalValueItem(XmlWriter writer, string value, bool isOptional)
            {
                writer.WriteStartElement(XmlSettings.XElementParamter);
                writer.WriteAttributeString(XmlSettings.XAttributeValue, value);
                writer.WriteAttributeString(XmlSettings.XAttributeOptional, isOptional.ToString().ToLower());
                writer.WriteEndElement();
            }
            private static void CreateXmlTypeItem(XmlWriter writer, string name, bool isOptional)
            {
                writer.WriteStartElement(XmlSettings.XElementParamter);
                writer.WriteAttributeString(XmlSettings.XAttributeName, name);
                writer.WriteAttributeString(XmlSettings.XAttributeOptional, isOptional.ToString().ToLower());
                writer.WriteEndElement();
            }
            private static void CreateXmlParameterItem(XmlWriter writer, string name, bool isOptional, bool hasValue, bool canBlank, bool isFordeEcaped)
            {
                writer.WriteStartElement(XmlSettings.XElementParamter);
                writer.WriteAttributeString(XmlSettings.XAttributeName, name);
                writer.WriteAttributeString(XmlSettings.XAttributeOptional, isOptional.ToString().ToLower());
                writer.WriteAttributeString(XmlSettings.XAttributeHasValue, hasValue.ToString().ToLower());
                writer.WriteAttributeString(XmlSettings.XAttributeAllowBlank, canBlank.ToString().ToLower());
                writer.WriteAttributeString(XmlSettings.XAttributeForceEscape, isFordeEcaped.ToString().ToLower());
                writer.WriteAttributeString(XmlSettings.XAttributeInfo, "");
                writer.WriteEndElement();
            }

            private static List<string> BuildStringList(XmlNode formatterNode, string xElement)
            {
                List<string> strings = new List<string>();
                foreach(XmlNode node in formatterNode.SelectNodes(xElement))
                {
                    string data = RepairXmlControlLiterals(node.Attributes?.GetNamedItem(XmlSettings.XAttributeValue)?.Value);
                    if (!IsKeyValid(data)) { throw new ArgumentException(string.Format("'{0}' is not a valid string parameter", data)); }
                    strings.Add(data);
                }
                return strings;
            }
            private static List<StringPairDefinition> BuildStringPairList(XmlNode formatterNode, string xElement)
            {
                List<StringPairDefinition> pairs = new List<StringPairDefinition>();
                foreach(XmlNode node in formatterNode.SelectNodes(xElement))
                {
                    pairs.Add(new StringPairDefinition(
                        RepairXmlControlLiterals(node.Attributes?.GetNamedItem(XmlSettings.XAttributeOpener)?.Value),
                        RepairXmlControlLiterals(node.Attributes?.GetNamedItem(XmlSettings.XAttributeCloser)?.Value)
                        ));
                }
                return pairs;
            }
            private static List<BlockValueDefinition> BuildMarkList(XmlNode fileNode, string xChapter)
            {
                List<BlockValueDefinition> preMarks = new List<BlockValueDefinition>();
                foreach(XmlNode node in fileNode.SelectSingleNode(xChapter)?.SelectNodes(XmlSettings.XElementParamter))
                {
                    preMarks.Add(
                        new BlockValueDefinition(
                            RepairXmlControlLiterals(node.Attributes?.GetNamedItem(XmlSettings.XAttributeValue)?.Value),
                            node.Attributes?.GetNamedItem(XmlSettings.XAttributeOptional)?.Value,
                            false));
                }
                return preMarks;
            }
            private static List<BlockValueDefinition> BuildBlockTypeList(XmlNode fileNode, string xChapter)
            {
                List<BlockValueDefinition> blockTypes = new List<BlockValueDefinition>();
                foreach(XmlNode node in fileNode.SelectSingleNode(xChapter)?.SelectNodes(XmlSettings.XElementParamter))
                {
                    blockTypes.Add(new BlockValueDefinition(
                        node.Attributes?.GetNamedItem(XmlSettings.XAttributeName)?.Value,
                        node.Attributes?.GetNamedItem(XmlSettings.XAttributeOptional)?.Value
                        ));
                }
                return blockTypes;
            }
            private static List<ItemDefinition> BuildItemList(XmlNode fileNode, string xChapter)
            {
                List<ItemDefinition> parameters = new List<ItemDefinition>();
                foreach (XmlNode node in fileNode.SelectSingleNode(xChapter)?.SelectNodes(XmlSettings.XElementParamter))
                {
                    parameters.Add(new ItemDefinition(
                        node.Attributes?.GetNamedItem(XmlSettings.XAttributeName)?.Value,
                        node.Attributes?.GetNamedItem(XmlSettings.XAttributeOptional)?.Value,
                        node.Attributes?.GetNamedItem(XmlSettings.XAttributeHasValue)?.Value,
                        node.Attributes?.GetNamedItem(XmlSettings.XAttributeAllowBlank)?.Value,
                        node.Attributes?.GetNamedItem(XmlSettings.XAttributeForceEscape)?.Value,
                        node.Attributes?.GetNamedItem(XmlSettings.XAttributeInfo)?.Value
                        ));
                }
                return parameters;
            }
        }
    } 
    
    public class EgsEcfFile
    {
        public string FilePath { get; private set; } = null;
        public string FileName { get; private set; } = null;
        public int LineCount { get; private set; } = 0;
        public bool LoadAbortPending { get; set; } = false;
        public bool HasUnsavedData { get; private set; } = true;

        public Encoding FileEncoding { get; private set; } = null;
        public EcfFileNewLineSymbols NewLineSymbol { get; private set; } = EcfFileNewLineSymbols.Unknown;
        public FormatDefinition Definition { get; private set; } = null;

        public ReadOnlyCollection<EcfStructureItem> ItemList { get; }

        private List<EcfStructureItem> InternalItemList { get; } = new List<EcfStructureItem>();
        private StringBuilder EcfLineBuilder { get; } = new StringBuilder();
        private List<EcfError> FatalFormatErrors { get; } = new List<EcfError>();

        public EgsEcfFile(string filePathAndName, FormatDefinition definition, Encoding encoding, EcfFileNewLineSymbols newLineSymbol)
        {
            ItemList = InternalItemList.AsReadOnly();
            FileName = Path.GetFileName(filePathAndName);
            FilePath = Path.GetDirectoryName(filePathAndName);
            LineCount = File.ReadLines(filePathAndName).Count();
            Definition = new FormatDefinition(definition);
            FileEncoding = encoding;
            NewLineSymbol = newLineSymbol;
        }
        public EgsEcfFile(string filePathAndName, FormatDefinition definition) : this(filePathAndName, definition, GetFileEncoding(filePathAndName), GetNewLineSymbol(filePathAndName))
        {
            
        }
        public EgsEcfFile(string filePathAndName) : this(filePathAndName, GetDefinition(Path.GetFileNameWithoutExtension(filePathAndName)))
        {
            
        }

        // file handling
        public List<EcfError> GetErrorList()
        {
            List<EcfError> errors = new List<EcfError>(FatalFormatErrors);
            errors.AddRange(ItemList.Where(item => item is EcfStructureItem).Cast<EcfStructureItem>().SelectMany(item => item.GetDeepErrorList()));
            return errors;
        }
        public List<T> GetDeepItemList<T>() where T : EcfStructureItem
        {
            return GetDeepItemList<T>(InternalItemList);
        }
        public static List<T> GetDeepItemList<T>(List<EcfStructureItem> itemList) where T : EcfStructureItem
        {
            List<T> list = new List<T>(itemList.Where(item => item is T).Cast<T>());
            list.AddRange(itemList.Where(item => item is EcfBlock).Cast<EcfBlock>().SelectMany(block => block.GetDeepChildList<T>()).ToList());
            return list;
        }
        public void Load()
        {
            Load(null);
        }
        public void Load(IProgress<int> lineProgress)
        {
            string filePathAndName = Path.Combine(FilePath, FileName);
            try
            {
                using (StreamReader reader = new StreamReader(File.Open(filePathAndName, FileMode.Open, FileAccess.Read), FileEncoding))
                {
                    ParseEcfContent(this, lineProgress, reader, out List<EcfStructureItem> rootItems, out List<EcfError> fatalErrors);
                }
            }
            catch (Exception ex)
            {
                throw new IOException(string.Format("File {0} content could not be loaded: {1}", filePathAndName, ex.Message));
            }
        }
        public void Save()
        {
            Save(Path.Combine(FilePath, FileName));
        }
        public void Save(string filePathAndName)
        {
            if (HasUnsavedData)
            {
                try
                {
                    string path = Path.GetDirectoryName(filePathAndName);
                    Directory.CreateDirectory(path);
                    RemoveInvalidItems();
                    FatalFormatErrors.Clear();
                    using (StreamWriter writer = new StreamWriter(File.Open(filePathAndName, FileMode.Create, FileAccess.Write), FileEncoding))
                    {
                        writer.NewLine = GetNewLineChar(NewLineSymbol);
                        CreateEcfContent(writer);
                    }
                    FileName = Path.GetFileName(filePathAndName);
                    FilePath = path;
                    LineCount = File.ReadLines(filePathAndName).Count();
                    HasUnsavedData = false;
                }
                catch (Exception ex)
                {
                    throw new IOException(string.Format("File {0} could not be saved: {1}", filePathAndName, ex.Message));
                }
            }
        }
        public void SetUnsavedDataFlag()
        {
            if (!HasUnsavedData)
            {
                HasUnsavedData = true;
            }
        }
        public bool AddItem(EcfStructureItem item)
        {
            if (item != null)
            {
                item.UpdateStructureData(this, null, -1);
                InternalItemList.Add(item);
                SetUnsavedDataFlag();
                return true;
            }
            return false;
        }
        public int AddItems(List<EcfStructureItem> items)
        {
            int count = 0;
            items?.ForEach(item =>
            {
                if (AddItem(item))
                {
                    count++;
                }
            });
            return count;
        }
        public bool AddItem(EcfStructureItem item, EcfStructureItem precedingItem)
        {
            if (item != null)
            {
                item.UpdateStructureData(this, null, -1);
                int index = InternalItemList.IndexOf(precedingItem);
                if (index < 0)
                {
                    InternalItemList.Add(item);
                }
                else
                {
                    InternalItemList.Insert(index + 1, item);
                }
                SetUnsavedDataFlag();
                return true;
            }
            return false;
        }
        public int AddItems(List<EcfStructureItem> items, EcfStructureItem precedingItem)
        {
            int count = 0;
            EcfStructureItem preItem = precedingItem;
            items?.ForEach(item =>
            {
                if (AddItem(item, preItem))
                {
                    count++;
                }
                preItem = item;
            });
            return count;
        }
        public bool RemoveItem(EcfStructureItem item)
        {
            if (item != null)
            {
                InternalItemList.Remove(item);
                SetUnsavedDataFlag();
                return true;
            }
            return false;
        }
        public int RemoveItems(List<EcfStructureItem> items)
        {
            int count = 0;
            items?.ForEach(item =>
            {
                if (RemoveItem(item))
                {
                    count++;
                }
            });
            return count;
        }

        // ecf creating
        private void RemoveInvalidItems()
        {
            InternalItemList.RemoveAll(item => item is EcfStructureItem comment && comment.GetDeepErrorList().Count > 0);
        }
        private void CreateEcfContent(StreamWriter writer)
        {
            int indent = 0;
            foreach (EcfBaseItem item in ItemList)
            {
                if (item is EcfComment comment)
                {
                    CreateCommentLine(writer, comment, indent);
                }
                else if (item is EcfBlock rootBlock)
                {
                    CreateBlock(writer, rootBlock, indent);
                }
            }
        }
        private void CreateCommentLine(StreamWriter writer, EcfComment comment, int indent)
        {
            CreateIndent(EcfLineBuilder, indent);
            EcfLineBuilder.Append(Definition.WritingSingleLineCommentStart);
            EcfLineBuilder.Append(Definition.MagicSpacer);
            EcfLineBuilder.Append(string.Join(" / ", comment.Comments));
            writer.WriteLine(EcfLineBuilder.ToString());
        }
        private void CreateBlock(StreamWriter writer, EcfBlock block, int indent)
        {
            CreateBlockStartLine(writer, block, indent);
            indent++;
            foreach (EcfBaseItem item in block.ChildItems)
            {
                if (item is EcfComment comment)
                {
                    CreateCommentLine(writer, comment, indent);
                }
                else if (item is EcfBlock childBlock)
                {
                    CreateBlock(writer, childBlock, indent);
                }
                else if (item is EcfParameter parameter)
                {
                    CreateParameterLine(writer, parameter, indent);
                }
            }
            indent--;
            CreateBlockEndLine(writer, indent);
        }
        private void CreateBlockStartLine(StreamWriter writer, EcfBlock block, int indent)
        {
            CreateIndent(EcfLineBuilder, indent);
            AppendBlockType(EcfLineBuilder, block);
            AppendAttributes(EcfLineBuilder, block.Attributes);
            AppendComments(EcfLineBuilder, block.Comments);
            writer.WriteLine(EcfLineBuilder.ToString());
        }
        private void CreateBlockEndLine(StreamWriter writer, int indent)
        {
            CreateIndent(EcfLineBuilder, indent);
            EcfLineBuilder.Append(Definition.WritingBlockIdentifierPair.Closer);
            writer.WriteLine(EcfLineBuilder.ToString());
        }
        private void CreateParameterLine(StreamWriter writer, EcfParameter parameter, int indent)
        {
            CreateIndent(EcfLineBuilder, indent);
            AppendParameter(EcfLineBuilder, parameter);
            AppendAttributes(EcfLineBuilder, parameter.Attributes);
            AppendComments(EcfLineBuilder, parameter.Comments);
            writer.WriteLine(EcfLineBuilder.ToString());
        }
        private void CreateIndent(StringBuilder lineBuilder, int indent)
        {
            lineBuilder.Clear();
            while (indent > 0)
            {
                lineBuilder.Append(Definition.MagicSpacer + Definition.MagicSpacer);
                indent--;
            }
        }
        private void AppendComments(StringBuilder lineBuilder, ReadOnlyCollection<string> comments)
        {
            if (comments.Count > 0)
            {
                lineBuilder.Append(Definition.MagicSpacer);
                lineBuilder.Append(Definition.WritingSingleLineCommentStart);
                lineBuilder.Append(Definition.MagicSpacer);
                lineBuilder.Append(string.Join(" / ", comments));
            }
        }
        private void AppendBlockType(StringBuilder lineBuilder, EcfBlock block)
        {
            lineBuilder.Append(Definition.WritingBlockIdentifierPair.Opener);
            lineBuilder.Append(Definition.MagicSpacer);
            lineBuilder.Append(block.PreMark ?? string.Empty);
            lineBuilder.Append(block.DataType ?? string.Empty);
            lineBuilder.Append(block.PostMark ?? string.Empty);
        }
        private void AppendAttributes(StringBuilder lineBuilder, ReadOnlyCollection<EcfAttribute> attributes)
        {
            List<string> attributeItems = attributes.Select(attr => CreateItem(attr)).ToList();
            lineBuilder.Append(string.Join((Definition.ItemSeperator + Definition.MagicSpacer), attributeItems));
        }
        private void AppendParameter(StringBuilder lineBuilder, EcfParameter parameter)
        {
            lineBuilder.Append(CreateItem(parameter));
            if (parameter.Attributes.Count > 0)
            {
                lineBuilder.Append(Definition.ItemSeperator);
                lineBuilder.Append(Definition.MagicSpacer);
            }
        }
        private string CreateItem(EcfKeyValueItem keyValueItem)
        {
            StringBuilder item = new StringBuilder(keyValueItem.Key);
            if (keyValueItem.ValueGroups.Any(group => group.Values.Count > 0))
            {
                item.Append(Definition.ItemValueSeperator);
                item.Append(Definition.MagicSpacer);
                bool escaped = false;

                if ((keyValueItem.Definition?.IsForceEscaped ?? false) || keyValueItem.IsUsingGroups() || keyValueItem.HasMultiValue())
                {
                    item.Append(Definition.WritingEscapeIdentifiersPair.Opener);
                    escaped = true;
                }
                item.Append(string.Join(Definition.ValueGroupSeperator, keyValueItem.ValueGroups.Where(group => group.Values.Count > 0)
                    .Select(group => string.Join(Definition.ValueSeperator, group.Values))));
                if (escaped)
                {
                    item.Append(Definition.WritingEscapeIdentifiersPair.Closer);
                }
            }
            return item.ToString();
        }

        // ecf parsing
        private void ParseEcfContent(EgsEcfFile file, IProgress<int> lineProgress, StreamReader reader, out List<EcfStructureItem> rootItems, out List<EcfError> fatalErrors)
        {
            rootItems = new List<EcfStructureItem>();
            fatalErrors = new List<EcfError>();  

            List<StackItem> stack = new List<StackItem>();
            string lineData;
            int lineCount = 0;
            int level = 0;
            List<string> comments = new List<string>();
            bool parameterLine;
            StringPairDefinition inCommentBlockPair = null;

            // parse content
            LoadAbortPending = false;
            while (!reader.EndOfStream)
            {
                // interprete next line
                if (LoadAbortPending) { break; }
                lineCount++;
                lineProgress?.Report(lineCount);
                lineData = TrimOuterPhrases(Definition, reader.ReadLine());
                if (!lineData.Equals(string.Empty))
                {
                    // comments
                    comments.Clear();
                    comments.AddRange(ParseComments(Definition, lineData, out lineData, inCommentBlockPair, out inCommentBlockPair));
                    if (lineData.Equals(string.Empty))
                    {
                        EcfComment comment = new EcfComment(comments);
                        if (level > 0)
                        {
                            stack[level - 1].Block.AddChild(comment);
                        }
                        else
                        {
                            comment.UpdateStructureData(file, null, -1);
                            rootItems.Add(comment);
                        }
                        continue;
                    }
                    // Block opener
                    StringPairDefinition blockIdPair = Definition.BlockIdentifierPairs.FirstOrDefault(pair => lineData.StartsWith(pair.Opener));
                    if (blockIdPair != null)
                    {
                        EcfBlock block = ParseBlockElement(Definition, level < 1, lineData, lineCount);
                        block.AddComments(comments);
                        level++;
                        if (stack.Count < level)
                        {
                            stack.Add(new StackItem(block, lineCount, lineData, blockIdPair));
                        }
                        else
                        {
                            stack[level - 1] = new StackItem(block, lineCount, lineData, blockIdPair);
                        }
                    }
                    // parameter or block closer
                    else if (level > 0)
                    {
                        StackItem stackItem = stack[level - 1];
                        parameterLine = false;
                        // parameter
                        if (!stackItem.BlockSymbolPair.Closer.Equals(lineData))
                        {
                            parameterLine = true;
                            EcfBlock block = stackItem.Block;
                            try
                            {
                                EcfParameter parameter = ParseParameter(Definition, lineData, block, lineCount);
                                parameter.AddComments(comments);
                            }
                            catch (EcfException ex)
                            {
                                fatalErrors.Add(new EcfError(ex.EcfError, string.Format("{0} / {1}", block.GetFullName(), ex.TextData), lineCount));
                            }
                        }
                        // block closer
                        if (lineData.EndsWith(stackItem.BlockSymbolPair.Closer))
                        {
                            level--;
                            EcfBlock block = stackItem.Block;
                            // completeness
                            List<EcfError> errors = CheckParametersValid(block.ChildItems.Where(child => child is EcfParameter)
                                .Cast<EcfParameter>().ToList(), Definition.BlockParameters);
                            errors.ForEach(error => error?.SetLineInFile(stackItem.LineNumber));
                            block.AddErrors(errors);
                            // comments
                            if (!parameterLine) { block.AddComments(comments); }
                            // append block to parent
                            if (level > 0)
                            {
                                StackItem parent = stack[level - 1];
                                parent.Block.AddChild(block);
                            }
                            // append block to root list
                            else
                            {
                                block.UpdateStructureData(file, null, -1);
                                rootItems.Add(block);
                            }
                        }
                    }
                    // reporting unassigned line or unopend block
                    else
                    {
                        if (Definition.BlockIdentifierPairs.Any(pair => lineData.StartsWith(pair.Closer) || lineData.EndsWith(pair.Closer)))
                        {
                            fatalErrors.Add(new EcfError(EcfErrors.BlockCloserWithoutOpener, lineData, lineCount));
                        }
                        else
                        {
                            fatalErrors.Add(new EcfError(EcfErrors.ParameterWithoutParent, lineData, lineCount));
                        }

                    }
                }
            }
            if (!LoadAbortPending)
            {
                // reporting unclosed blocks 
                while (level > 0)
                {
                    StackItem item = stack[level - 1];
                    fatalErrors.Add(new EcfError(EcfErrors.BlockOpenerWithoutCloser, string.Format("{0} / {1}", item.Block.GetFullName(), item.LineData), item.LineNumber));
                    level--;
                }

                // global error checks
                List<EcfBlock> completeBlockList = GetDeepItemList<EcfBlock>(rootItems);
                completeBlockList.ForEach(block => block.RevalidateUniqueness(completeBlockList));
                ParseReferenceInheritance(completeBlockList);

                // update der daten
                InternalItemList.Clear();
                FatalFormatErrors.Clear();
                AddItems(rootItems);
                FatalFormatErrors.AddRange(fatalErrors);
            }
        }
        private static List<string> ParseComments(FormatDefinition definition, string inLineData, out string outLineData, 
            StringPairDefinition inCommentBlockPair, out StringPairDefinition outCommentBlockPair)
        {
            List<string> comments = new List<string>();
            comments.AddRange(ParseBlockComment(definition, inLineData, out inLineData, inCommentBlockPair, out inCommentBlockPair));
            comments.AddRange(ParseSingleLineComment(definition, inLineData, out inLineData));
            comments.AddRange(ParseInLineComment(definition, inLineData, out inLineData));
            comments.AddRange(ParseMultiLineComment(definition, inLineData, out inLineData, inCommentBlockPair, out inCommentBlockPair));
            outLineData = inLineData;
            outCommentBlockPair = inCommentBlockPair;
            return comments;
        }
        private static List<string> ParseBlockComment(FormatDefinition definition, string inLineData, out string outLineData, 
            StringPairDefinition inCommentBlockPair, out StringPairDefinition outCommentBlockPair)
        {
            List<string> comments = new List<string>();
            if (inCommentBlockPair != null)
            {
                int end = inLineData.IndexOf(inCommentBlockPair.Closer);
                if (end >= 0)
                {
                    comments.Add(TrimComment(definition, inLineData.Substring(0, end)));
                    inLineData = inLineData.Remove(0, end + inCommentBlockPair.Closer.Length).Trim();
                    inCommentBlockPair = null;
                }
                else
                {
                    comments.Add(TrimComment(definition, inLineData));
                    inLineData = "";
                }
            }
            outLineData = inLineData;
            outCommentBlockPair = inCommentBlockPair;
            return comments;
        }
        private static List<string> ParseSingleLineComment(FormatDefinition definition, string inLineData, out string outLineData)
        {
            List<string> comments = new List<string>();
            string singleLineMark = definition.SingleLineCommentStarts.Where(mark => inLineData.IndexOf(mark) >= 0).OrderByDescending(mark => inLineData.IndexOf(mark)).FirstOrDefault();
            if (singleLineMark != null)
            {
                int start = inLineData.IndexOf(singleLineMark);
                comments.Add(TrimComment(definition, inLineData.Substring(start).Remove(0, singleLineMark.Length)));
                inLineData = inLineData.Remove(start).Trim();
            }
            outLineData = inLineData;
            return comments;
        }
        private static List<string> ParseInLineComment(FormatDefinition definition, string inLineData, out string outLineData)
        {
            List<string> comments = new List<string>();
            StringPairDefinition inLineMark = null;
            do
            {
                inLineMark = definition.MultiLineCommentPairs.FirstOrDefault(pair => {
                    int start = inLineData.IndexOf(pair.Opener);
                    if (start >= 0)
                    {
                        int end = inLineData.IndexOf(pair.Closer, start);
                        if (end >= 0)
                        {
                            comments.Add(TrimComment(definition, inLineData.Substring(start + pair.Opener.Length, end - start - pair.Opener.Length)));
                            inLineData = inLineData.Remove(start, end - start + pair.Closer.Length).Trim();
                            return true;
                        }
                    }
                    return false;
                });
            } while (inLineMark != null);
            outLineData = inLineData;
            return comments;
        }
        private static List<string> ParseMultiLineComment(FormatDefinition definition, string inLineData, out string outLineData, 
            StringPairDefinition inCommentBlockPair, out StringPairDefinition outCommentBlockPair)
        {
            List<string> comments = new List<string>();
            StringPairDefinition multiLineMark;
            multiLineMark = definition.MultiLineCommentPairs.Where(pair => inLineData.IndexOf(pair.Closer) >= 0)
                .OrderByDescending(pair => inLineData.IndexOf(pair.Closer)).LastOrDefault();
            if (multiLineMark != null)
            {
                int end = inLineData.IndexOf(multiLineMark.Closer);
                if (end >= 0)
                {
                    comments.Add(TrimComment(definition, inLineData.Substring(0, end)));
                    inLineData = inLineData.Remove(0, end + multiLineMark.Closer.Length).Trim();
                    inCommentBlockPair = null;
                }
            }
            multiLineMark = definition.MultiLineCommentPairs.Where(pair => inLineData.IndexOf(pair.Opener) >= 0)
                .OrderByDescending(pair => inLineData.IndexOf(pair.Opener)).FirstOrDefault();
            if (multiLineMark != null)
            {
                int start = inLineData.IndexOf(multiLineMark.Opener);
                if (start >= 0)
                {
                    comments.Add(TrimComment(definition, inLineData.Substring(start + multiLineMark.Opener.Length)));
                    inLineData = inLineData.Remove(start).Trim();
                    inCommentBlockPair = multiLineMark;
                }
            }
            outLineData = inLineData;
            outCommentBlockPair = inCommentBlockPair;
            return comments;
        }
        private static EcfBlock ParseBlockElement(FormatDefinition definition, bool isRoot, string lineData, int lineInFile)
        {
            ReadOnlyCollection<ItemDefinition> attributeDefinitions = isRoot ? definition.RootBlockAttributes : definition.ChildBlockAttributes;
            ReadOnlyCollection<BlockValueDefinition> dataTypeDefinitions = isRoot ? definition.RootBlockTypes : definition.ChildBlockTypes;

            lineData = TrimPairs(lineData, definition.BlockIdentifierPairs);
            string preMark = ParseBlockPreMark(definition, lineData);
            string blockType = ParseBlockType(definition, lineData, preMark, out string postMark);
            lineData = RemoveBlockType(lineData, preMark, blockType, postMark);
            Queue<string> splittedItems = SplitEcfItems(definition, lineData);
            EcfBlock block = new EcfBlock(preMark, blockType, postMark);
            List<EcfAttribute> attributes = ParseAttributes(definition, splittedItems, attributeDefinitions, lineInFile);
            block.AddAttributes(attributes);
            List<EcfError> errors = new List<EcfError>();
            errors.AddRange(CheckBlockPostMark(preMark, definition.BlockTypePreMarks));
            errors.AddRange(CheckBlockDataType(blockType, dataTypeDefinitions));
            errors.AddRange(CheckBlockPostMark(postMark, definition.BlockTypePostMarks));
            errors.AddRange(CheckAttributesValid(attributes, attributeDefinitions));
            errors.ForEach(error => error?.SetLineInFile(lineInFile));
            block.AddErrors(errors);
            return block;
        }
        private static EcfParameter ParseParameter(FormatDefinition definition, string lineData, EcfBlock block, int lineInFile)
        {
            lineData = TrimPairs(lineData, definition.BlockIdentifierPairs);
            Queue<string> splittedItems = SplitEcfItems(definition, lineData);
            EcfParameter parameter = ParseParameterBase(definition, splittedItems, block, lineInFile);
            List<EcfAttribute> attributes = ParseAttributes(definition, splittedItems, definition.ParameterAttributes, lineInFile);
            parameter.AddAttributes(attributes);
            List<EcfError> errors = CheckAttributesValid(attributes, definition.ParameterAttributes);
            errors.ForEach(error => error?.SetLineInFile(lineInFile));
            parameter.AddErrors(errors);
            return parameter;
        }
        private static string ParseBlockPreMark(FormatDefinition definition, string lineData)
        {
            return definition.BlockTypePreMarks.Where(mark => !string.IsNullOrEmpty(mark.Value)).FirstOrDefault(mark => lineData.StartsWith(mark.Value))?.Value;
        }
        private static string ParseBlockType(FormatDefinition definition, string lineData, string preMark, out string postMark)
        {
            postMark = null;
            if (preMark != null) { lineData = lineData.Remove(0, preMark.Length); }
            StringBuilder blockTypeItem = new StringBuilder();
            StringPairDefinition escapePair = null;
            string buffer;
            for (int index = 0; index < lineData.Length; index++)
            {
                buffer = lineData[index].ToString();
                if (escapePair == null)
                {
                    escapePair = definition.EscapeIdentifiersPairs.FirstOrDefault(pair => pair.Opener.Equals(buffer));
                    if (escapePair == null && definition.BlockTypePostMarks.Where(mark => !string.IsNullOrEmpty(mark.Value)).Any(mark => mark.Value.Equals(buffer)))
                    {
                        postMark = buffer;
                        break;
                    }
                }
                else if (escapePair.Closer.Equals(buffer))
                {
                    escapePair = null;
                }
                blockTypeItem.Append(buffer);
            }
            return blockTypeItem.Length > 0 ? blockTypeItem.ToString() : null;
        }
        private static  List<EcfAttribute> ParseAttributes(FormatDefinition definition, Queue<string> splittedItems, ReadOnlyCollection<ItemDefinition> definedAttributes, int lineInFile)
        {
            List<EcfError> errors = new List<EcfError>();
            List<EcfAttribute> attributes = new List<EcfAttribute>();
            while (splittedItems.Count > 0)
            {
                string key = splittedItems.Dequeue();
                errors.Clear();
                List<EcfValueGroup> groups = null;
                errors.Add(CheckItemUnknown(definedAttributes, key, KeyValueItemTypes.Attribute, out ItemDefinition itemDefinition));
                if (itemDefinition != null && itemDefinition.HasValue)
                {
                    if (splittedItems.Count > 0)
                    {
                        groups = ParseValues(definition, splittedItems.Dequeue());
                    }
                    errors.AddRange(CheckValuesValid(groups, itemDefinition, definition));
                }
                EcfAttribute attr = new EcfAttribute(key);
                attributes.Add(attr);
                attr.AddValues(groups);
                errors.ForEach(error => error?.SetLineInFile(lineInFile));
                attr.AddErrors(errors);
            }
            return attributes;
        }
        private static EcfParameter ParseParameterBase(FormatDefinition definition, Queue<string> splittedItems, EcfBlock block, int lineInFile)
        {
            string key = null;
            List<EcfValueGroup> groups = null;
            List<EcfError> errors = new List<EcfError>();
            ItemDefinition itemDefinition = null;
            if (splittedItems.Count > 0)
            {
                key = splittedItems.Dequeue();
                errors.Add(CheckItemUnknown(definition.BlockParameters, key, KeyValueItemTypes.Parameter, out itemDefinition));
                if (itemDefinition != null && itemDefinition.HasValue)
                {
                    if (splittedItems.Count > 0)
                    {
                        groups = ParseValues(definition, splittedItems.Dequeue());
                    }
                    errors.AddRange(CheckValuesValid(groups, itemDefinition, definition));
                }
            }
            EcfParameter parameter = new EcfParameter(key, groups, null);
            block.AddChild(parameter);
            errors.ForEach(error => error?.SetLineInFile(lineInFile));
            parameter.AddErrors(errors);
            return parameter;
        }
        private static List<EcfValueGroup> ParseValues(FormatDefinition definition, string itemValue)
        {
            List<EcfValueGroup> groups = new List<EcfValueGroup>();
            string[] valueGroups = TrimPairs(itemValue, definition.EscapeIdentifiersPairs)?.Split(definition.ValueGroupSeperator.ToArray());
            if (valueGroups != null)
            {
                foreach (string groupValues in valueGroups)
                {
                    groups.Add(new EcfValueGroup(groupValues.Trim().Split(definition.ValueSeperator.ToArray()).Select(value => value.Trim()).ToList()));
                }
            }
            return groups;
        }
        private static void ParseReferenceInheritance(List<EcfBlock> blockList)
        {
            blockList.ForEach(block => 
            {
                block.AddError(CheckBlockReferenceValid(block, blockList, out EcfBlock inheriter));
                block.Inheritor = inheriter;
            });
        }
        private static string TrimStarts(string lineData, ReadOnlyCollection<string> definedStarts)
        {
            string startMark = null;
            if (lineData != null)
            {
                do
                {
                    startMark = definedStarts.FirstOrDefault(start => lineData.StartsWith(start));
                    if (startMark != null)
                    {
                        lineData = lineData.Remove(0, startMark.Length).Trim();
                    }
                } while (startMark != null);
            }
            return lineData?.Trim();
        }
        private static string TrimPairs(string lineData, ReadOnlyCollection<StringPairDefinition> definedIdentifiers)
        {
            StringPairDefinition blockPair = null;
            if (lineData != null)
            {
                do
                {
                    blockPair = definedIdentifiers.FirstOrDefault(pair => lineData.StartsWith(pair.Opener));
                    if (blockPair != null)
                    {
                        lineData = lineData.Remove(0, blockPair.Opener.Length).Trim();
                    }
                } while (blockPair != null);
                do
                {
                    blockPair = definedIdentifiers.FirstOrDefault(pair => lineData.EndsWith(pair.Closer));
                    if (blockPair != null)
                    {
                        lineData = lineData.Remove(lineData.Length - blockPair.Closer.Length, blockPair.Closer.Length).Trim();
                    }
                } while (blockPair != null);
            }
            return lineData?.Trim();
        }
        private static string TrimComment(FormatDefinition definition, string comment)
        {
            if (comment != null)
            {
                comment = TrimStarts(comment.Trim(), definition.SingleLineCommentStarts).Trim();
                comment = TrimPairs(comment, definition.MultiLineCommentPairs).Trim();
            }
            return comment;
        }
        private static string TrimOuterPhrases(FormatDefinition definition, string lineData)
        {
            string foundPhrase;
            if (lineData != null)
            {
                do
                {
                    foundPhrase = definition.OuterTrimmingPhrases.FirstOrDefault(phrase => lineData.StartsWith(phrase));
                    if (foundPhrase != null)
                    {
                        lineData = lineData.Remove(0, foundPhrase.Length).Trim();
                    }
                } while (foundPhrase != null);
                do
                {
                    foundPhrase = definition.OuterTrimmingPhrases.FirstOrDefault(phrase => lineData.EndsWith(phrase));
                    if (foundPhrase != null)
                    {
                        lineData = lineData.Remove(lineData.Length - foundPhrase.Length, foundPhrase.Length).Trim();
                    }
                } while (foundPhrase != null);
            }
            return lineData?.Trim();
        }
        private static string RemoveBlockType(string lineData, string preMark, string blockType, string postMark)
        {
            return lineData.Remove(0, (preMark?.Length ?? 0) + (blockType?.Length ?? 0) + (postMark?.Length ?? 0)).Trim();
        }
        private static Queue<string> SplitEcfItems(FormatDefinition definition, string lineData)
        {
            Queue<string> splittedData = new Queue<string>();
            StringBuilder dataPart = new StringBuilder();
            StringPairDefinition escapePair = null;
            string buffer;
            bool split = false;

            // walk char by char
            for (int index = 0; index < lineData.Length; index++)
            {
                buffer = lineData[index].ToString();
                split = false;
                if (escapePair == null)
                {
                    escapePair = definition.EscapeIdentifiersPairs.FirstOrDefault(pair => pair.Opener.Equals(buffer));
                    if (escapePair == null)
                    {
                        split = ((buffer == definition.ItemSeperator) || (buffer == definition.ItemValueSeperator));
                    }
                }
                else if (escapePair.Closer.Equals(buffer))
                {
                    escapePair = null;
                }
                if (split)
                {
                    splittedData.Enqueue(dataPart.ToString().Trim());
                    dataPart.Clear();
                }
                else
                {
                    dataPart.Append(buffer);
                }
            }
            if (dataPart.Length > 0 || split)
            {
                splittedData.Enqueue(dataPart.ToString().Trim());
            }
            return splittedData;
        }

        private class StackItem
        {
            public EcfBlock Block { get; }
            public int LineNumber { get; }
            public string LineData { get; }
            public StringPairDefinition BlockSymbolPair { get; }
            public StackItem(EcfBlock block, int lineNumber, string lineData, StringPairDefinition blockSymbolPair)
            {
                Block = block;
                LineNumber = lineNumber;
                LineData = lineData;
                BlockSymbolPair = blockSymbolPair;
            }
        }
    }

    // Ecf Error Handling
    public enum EcfErrors
    {
        Unknown,
        KeyNullOrEmpty,
        
        BlockIdNotUnique,
        BlockInheritorMissing,
        BlockOpenerWithoutCloser,
        BlockCloserWithoutOpener,
        BlockDataTypeMissing,
        BlockDataTypeUnknown,
        BlockPreMarkMissing,
        BlockPreMarkUnknown,
        BlockPostMarkMissing,
        BlockPostMarkUnknown,

        ParameterUnknown,
        ParameterWithoutParent,
        ParameterMissing,
        ParameterDoubled,

        AttributeUnknown,
        AttributeMissing,
        AttributeDoubled,

        ValueGroupEmpty,
        ValueGroupIndexInvalid,

        ValueNull,
        ValueEmpty,
        ValueIndexInvalid,
        ValueContainsProhibitedPhrases,
    }
    public class EcfError
    {
        public EcfErrors Type { get; }
        public string Info { get; }
        public bool IsFromParsing { get; private set; }

        public EcfStructureItem Item { get; set; } = null;
        public int LineInFile { get; private set; } = 0;

        public EcfError(EcfErrors type, string info)
        {
            Type = type;
            Info = info ?? "null";
        }
        public EcfError(EcfErrors type, string info, int lineInFile) : this(type, info)
        {
            SetLineInFile(lineInFile);
        }

        // copyconstructor
        public EcfError(EcfError template, EcfStructureItem reference)
        {
            Type = template.Type;
            Info = template.Info;
            SetLineInFile(template.LineInFile);
            Item = reference;
        }

        public void SetLineInFile(int lineInFile)
        {
            LineInFile = lineInFile;
            IsFromParsing = lineInFile > 0;
        }
        public override string ToString()
        {
            StringBuilder errorText = new StringBuilder();
            if (IsFromParsing)
            {
                errorText.Append("In Line ");
                errorText.Append(LineInFile.ToString());
                errorText.Append(" at '");
            }
            else
            {
                errorText.Append("At '");
            }
            errorText.Append(Item != null ? Item.GetFullName() : "unknown");
            errorText.Append("' occured error ");
            errorText.Append(Type.ToString());
            errorText.Append(", additional info: '");
            errorText.Append(Info);
            errorText.Append("'");
            return errorText.ToString();
        }
    }
    public class EcfException : Exception
    {
        public EcfErrors EcfError { get; }
        public string TextData { get; }


        public EcfException() : base(ToString(EcfErrors.Unknown, ""))
        {
            EcfError = EcfErrors.Unknown;
            TextData = "";
        }

        public EcfException(EcfErrors ecfError) : base(ToString(ecfError, ""))
        {
            EcfError = ecfError;
            TextData = "";
        }

        public EcfException(EcfErrors ecfError, string textData) : base(ToString(ecfError, textData))
        {
            EcfError = ecfError;
            TextData = textData ?? "null";
        }

        public EcfException(EcfErrors ecfError, string textData, Exception inner) : base(ToString(ecfError, textData), inner)
        {
            EcfError = ecfError;
            TextData = textData ?? "null";
        }

        public override string ToString()
        {
            return ToString(EcfError, TextData);
        }
        private static string ToString(EcfErrors ecfError, string textData)
        {
            return string.Format("{0} in: '{1}'", ecfError, textData);
        }
    }

    // ecf data Structure Classes
    public abstract class EcfBaseItem
    {
        protected EgsEcfFile EcfFile { get; private set; } = null;
        public EcfStructureItem Parent { get; private set; } = null;
        public int StructureLevel { get; private set; } = -1;

        public EcfBaseItem()
        {
            
        }

        // copy constructor
        public EcfBaseItem(EcfBaseItem template)
        {
            EcfFile = template.EcfFile;
            Parent = template.Parent;
            StructureLevel = template.StructureLevel;
        }

        // publics
        public abstract override string ToString();
        public bool IsRoot()
        {
            return Parent == null;
        }
        public void UpdateStructureData(EgsEcfFile file, EcfStructureItem parent, int level)
        {
            EcfFile = file;
            Parent = parent;
            StructureLevel = level + 1;
            OnStructureDataUpdate();
        }
        public static EcfBaseItem GetRootItem(EcfBaseItem item)
        {
            EcfBaseItem parent;
            if (item?.IsRoot() ?? true)
            {
                parent = item;
            }
            else
            {
                parent = GetRootItem(item?.Parent);
            }
            return parent;
        }

        // private
        protected abstract void OnStructureDataUpdate();
    }
    public abstract class EcfStructureItem : EcfBaseItem
    {
        protected string DefaultName { get; }
        private List<EcfError> InternalErrors { get; } = new List<EcfError>();
        public ReadOnlyCollection<EcfError> Errors { get; }
        private List<string> InternalComments { get; } = new List<string>();
        public ReadOnlyCollection<string> Comments { get; }

        public EcfStructureItem(string defaultName) : base()
        {
            DefaultName = defaultName;
            Errors = InternalErrors.AsReadOnly();
            Comments = InternalComments.AsReadOnly();
        }

        // copy constructor
        public EcfStructureItem(EcfStructureItem template) : base(template)
        {
            DefaultName = template.DefaultName;
            Errors = InternalErrors.AsReadOnly();
            Comments = InternalComments.AsReadOnly();

            AddErrors(template.Errors.Cast<EcfError>().Select(error => new EcfError(error, this)).ToList());
            AddComments(template.InternalComments);
        }

        public abstract List<EcfError> GetDeepErrorList();
        public abstract string GetFullName();
        public abstract string BuildIdentification();
        public abstract int Revalidate();

        public EcfStructureItem BuildDeepCopy()
        {
            switch (this)
            {
                case EcfComment comment: return new EcfComment(comment);
                case EcfAttribute attribute: return new EcfAttribute(attribute);
                case EcfParameter parameter: return new EcfParameter(parameter);
                case EcfBlock block: return new EcfBlock(block);
                default: throw new NotImplementedException(string.Format("DeepCopy not implemented for {0}", GetType().ToString()));
            }
        }
        public int GetIndexInStructureLevel()
        {
            if (IsRoot())
            {
                return EcfFile.ItemList.IndexOf(this);
            }
            else
            {
                return (Parent as EcfBlock)?.ChildItems.IndexOf(this) ?? -1;
            }
        }
        public int GetIndexInStructureLevel<T>() where T : EcfStructureItem
        {
            List<EcfStructureItem> items;
            if (IsRoot())
            {
                items = EcfFile.ItemList.Where(item => item is EcfStructureItem).Cast<EcfStructureItem>().ToList();
            }
            else
            {
                items = (Parent as EcfBlock)?.ChildItems.Where(item => item is EcfStructureItem).Cast<EcfStructureItem>().ToList();
            }
            return items?.Where(child => child is T).Cast<T>().ToList().IndexOf((T)this) ?? -1;
        }
        public bool AddError(EcfError error)
        {
            if (error != null)
            {
                error.Item = this;
                InternalErrors.Add(error);
                return true;
            }
            return false;
        }
        public int AddErrors(List<EcfError> errors)
        {
            int count = 0;
            errors?.ForEach(error => {
                if (AddError(error))
                {
                    count++;
                }
            });
            return count;
        }
        protected bool RemoveError(EcfError error)
        {
            if (error != null)
            {
                InternalErrors.Remove(error);
                return true;
            }
            return false;
        }
        protected int RemoveErrors(List<EcfError> errors)
        {
            int count = 0;
            errors?.ForEach(error => {
                if (RemoveError(error))
                {
                    count++;
                }
            });
            return count;
        }
        public int RemoveErrors(EcfErrors error)
        {
            return RemoveErrors(Errors.Where(err => err.Type.Equals(error)).ToList());
        }
        public int RemoveErrors(params EcfErrors[] errors)
        {
            int count = 0;
            foreach (EcfErrors error in errors)
            {
                count += RemoveErrors(error);
            }
            return count;
        }
        public bool ContainsError(EcfErrors error)
        {
            return InternalErrors.Any(err => err.Type.Equals(error));
        }
        public bool AddComment(string text)
        {
            if (IsKeyValid(text))
            {
                InternalComments.Add(text);
                EcfFile?.SetUnsavedDataFlag();
                return true;
            }
            return false;
        }
        public int AddComments(List<string> comments)
        {
            int count = 0;
            comments?.ForEach(comment => {
                if (AddComment(comment))
                {
                    count++;
                }
            });
            return count;
        }
        public void ClearComments()
        {
            InternalComments.Clear();
        }
    }
    public abstract class EcfKeyValueItem : EcfStructureItem
    {
        public string Key { get; private set; }
        public ReadOnlyCollection<ItemDefinition> DefinitionGroup { get; private set; } = null;
        public ItemDefinition Definition { get; private set; } = null;

        private List<EcfValueGroup> InternalValueGroups { get; } = new List<EcfValueGroup>();
        public ReadOnlyCollection<EcfValueGroup> ValueGroups { get; }

        private KeyValueItemTypes ItemType { get; set; } = KeyValueItemTypes.Unknown;

        public enum KeyValueItemTypes
        {
            Unknown,
            Parameter,
            Attribute,
        }

        public EcfKeyValueItem(string key, KeyValueItemTypes itemType, string defaultName) : base(defaultName)
        {
            UpdateKey(key);
            ItemType = itemType;
            ValueGroups = InternalValueGroups.AsReadOnly();
        }

        // copy constructor
        public EcfKeyValueItem(EcfKeyValueItem template) : base (template)
        {
            Key = template.Key;
            Definition = Definition == null ? null : new ItemDefinition(template.Definition);
            DefinitionGroup = template.DefinitionGroup.Select(def => new ItemDefinition(def)).ToList().AsReadOnly();
            ValueGroups = InternalValueGroups.AsReadOnly();
            ItemType = template.ItemType;
            AddValues(template.ValueGroups.Select(group => new EcfValueGroup(group)).ToList());
        }

        public override string BuildIdentification()
        {
            return string.Format("{0} {1}", DefaultName, Key);
        }
        public void UpdateKey(string key)
        {
            CheckKey(key);
            Key = key;
        }
        public void UpdateDefinition(ReadOnlyCollection<ItemDefinition> definitionGroup)
        {
            DefinitionGroup = definitionGroup;
            if (DefinitionGroup == null) { Definition = null; return; }
            CheckItemUnknown(definitionGroup, Key, ItemType, out ItemDefinition itemDefinition);
            Definition = itemDefinition;
        }
        protected int RevalidateKeyValue()
        {
            if (DefinitionGroup == null) { throw new InvalidOperationException("Validation is only possible with File reference"); }

            int errorCount = 0;
            errorCount += RevalidateKey() ? 1 : 0;
            if (Definition != null) { errorCount += RevalidateValues(); }
            return errorCount;
        }
        private bool RevalidateKey()
        {
            RemoveErrors(EcfErrors.ParameterUnknown, EcfErrors.AttributeUnknown);
            bool result = AddError(CheckItemUnknown(DefinitionGroup, Key, ItemType, out ItemDefinition itemDefinition));
            Definition = itemDefinition;
            return result;
        }
        private int RevalidateValues()
        {
            RemoveErrors(EcfErrors.ValueGroupEmpty, EcfErrors.ValueNull, EcfErrors.ValueEmpty, EcfErrors.ValueContainsProhibitedPhrases);
            return AddErrors(CheckValuesValid(InternalValueGroups, Definition, EcfFile?.Definition));
        }
        public bool IsUsingGroups()
        {
            return InternalValueGroups.Count(group => group.Values.Count > 0) > 1;
        }
        public bool HasValue()
        {
            return (InternalValueGroups.FirstOrDefault()?.Values.Count ?? 0) > 0;
        }
        public bool HasMultiValue()
        {
            return InternalValueGroups.Any(group => group.Values.Count > 1);
        }
        public bool AddValue(string value)
        {
            if (InternalValueGroups.Count == 0)
            {
                EcfValueGroup group = new EcfValueGroup();
                group.UpdateStructureData(EcfFile, this, StructureLevel);
                InternalValueGroups.Add(group);
            }
            return AddValue(value, 0);
        }
        public bool AddValue(string value, int groupIndex)
        {
            if (groupIndex < 0 || groupIndex >= InternalValueGroups.Count) { throw new EcfException(EcfErrors.ValueGroupIndexInvalid, groupIndex.ToString()); }
            return InternalValueGroups[groupIndex].AddValue(value);
        }
        public int AddValues(List<string> values)
        {
            int count = 0;
            values?.ForEach(value => {
                if (AddValue(value))
                {
                    count++;
                }
            });
            return count;
        }
        public int AddValues(List<string> values, int groupIndex)
        {
            if (groupIndex < 0 || groupIndex >= InternalValueGroups.Count) { throw new EcfException(EcfErrors.ValueGroupIndexInvalid, groupIndex.ToString()); }
            return InternalValueGroups[groupIndex].AddValues(values);
        }
        public bool AddValues(EcfValueGroup valueGroup)
        {
            if (valueGroup != null) {
                valueGroup.UpdateStructureData(EcfFile, this, StructureLevel);
                InternalValueGroups.Add(valueGroup);
                EcfFile?.SetUnsavedDataFlag();
                return true;
            }
            return false;
        }
        public int AddValues(List<EcfValueGroup> valueGroups)
        {
            int count = 0;
            valueGroups?.ForEach(group => {
                if (AddValues(group))
                {
                    count++;
                }
            });
            return count;
        }
        public ReadOnlyCollection<string> GetAllValues()
        {
            return InternalValueGroups.SelectMany(group => group.Values).ToList().AsReadOnly();
        }
        public string GetFirstValue()
        {
            try
            {
                return GetValue(0);
            }
            catch(Exception)
            {
                return null;
            }
        }
        public string GetValue(int valueIndex)
        {
            try { 
                return GetValue(valueIndex, 0);
            }
            catch(Exception)
            {
                return null;
            }
}
        public string GetValue(int valueIndex, int groupIndex)
        {
            if (groupIndex < 0 || groupIndex >= InternalValueGroups.Count) { throw new EcfException(EcfErrors.ValueGroupIndexInvalid, groupIndex.ToString()); }
            if (valueIndex < 0 || valueIndex >= InternalValueGroups[groupIndex].Values.Count) { throw new EcfException(EcfErrors.ValueIndexInvalid, valueIndex.ToString()); }
            return InternalValueGroups[groupIndex].Values[valueIndex];
        }
        public void ClearValues()
        {
            InternalValueGroups.Clear();
        }
        public int IndexOf(EcfValueGroup group)
        {
            return InternalValueGroups.IndexOf(group);
        }

        private void CheckKey(string key)
        {
            if (!IsKeyValid(key)) { throw new EcfException(EcfErrors.KeyNullOrEmpty, GetType().Name); }
        }
    }
    public class EcfAttribute : EcfKeyValueItem
    {
        public EcfAttribute(string key) : base(key, KeyValueItemTypes.Attribute, "Attribute")
        {
            
        }
        public EcfAttribute(string key, string value) : this(key)
        {
            AddValue(value);
        }
        public EcfAttribute(string key, List<string> values) : this(key)
        {
            AddValues(values);
        }
        public EcfAttribute(string key, List<EcfValueGroup> valueGroups) : this(key)
        {
            AddValues(valueGroups);
        }

        // copyconstructor
        public EcfAttribute(EcfAttribute template) : base(template)
        {

        }

        public override int Revalidate()
        {
            return RevalidateKeyValue();
        }
        public override string ToString()
        {
            return string.Format("{0}, values: '{1}'", BuildIdentification(), ValueGroups.Sum(group => group.Values.Count).ToString());
        }
        public override List<EcfError> GetDeepErrorList()
        {
            return Errors.ToList();
        }
        public override string GetFullName()
        {
            StringBuilder name = new StringBuilder();
            if (!IsRoot())
            {
                name.Append(Parent.GetFullName());
                name.Append(" / ");
            }
            name.Append(BuildIdentification());
            return name.ToString();
        }

        protected override void OnStructureDataUpdate()
        {
            UpdateDefinition(EcfFile?.Definition.BlockParameters);
        }
    }
    public class EcfParameter : EcfKeyValueItem
    {
        private List<EcfAttribute> InternalAttributes { get; } = new List<EcfAttribute>();
        public ReadOnlyCollection<EcfAttribute> Attributes { get; }

        public EcfParameter(string key) : base(key, KeyValueItemTypes.Parameter, "Parameter")
        {
            Attributes = InternalAttributes.AsReadOnly();
        }
        public EcfParameter(string key, List<string> values, List<EcfAttribute> attributes) : this(key)
        {
            AddValues(values);
            AddAttributes(attributes);
        }
        public EcfParameter(string key, List<EcfValueGroup> valueGroups, List<EcfAttribute> attributes) : this(key)
        {
            AddValues(valueGroups);
            AddAttributes(attributes);
        }
        
        // copy constructor
        public EcfParameter(EcfParameter template) : base(template)
        {
            Attributes = InternalAttributes.AsReadOnly();

            AddAttributes(template.Attributes.Select(attribute => new EcfAttribute(attribute)).ToList());
        }

        public override string ToString()
        {
            return string.Format("{0}, values: '{1}' and attributes: '{2}'", BuildIdentification(), ValueGroups.Sum(group => group.Values.Count).ToString(), Attributes.Count);
        }
        public override string GetFullName()
        {
            StringBuilder name = new StringBuilder();
            if (!IsRoot())
            {
                name.Append(Parent.GetFullName());
                name.Append(" / ");
            }
            name.Append(BuildIdentification());
            return name.ToString();
        }
        public override List<EcfError> GetDeepErrorList()
        {
            List<EcfError> errors = new List<EcfError>(Errors);
            errors.AddRange(InternalAttributes.SelectMany(attribute => attribute.GetDeepErrorList()));
            return errors;
        }
        public override int Revalidate()
        {
            int errorCount = RevalidateKeyValue();
            errorCount += RevalidateAttributes();
            errorCount += InternalAttributes.Sum(attr => attr.Revalidate());
            return errorCount;
        }
        public int RevalidateAttributes()
        {
            if (DefinitionGroup == null) { throw new InvalidOperationException("Validation is only possible with File reference"); }

            RemoveErrors(EcfErrors.AttributeMissing, EcfErrors.AttributeDoubled);
            return AddErrors(CheckAttributesValid(InternalAttributes, DefinitionGroup));
        }
        public bool AddAttribute(EcfAttribute attribute)
        {
            if (attribute != null)
            {
                attribute.UpdateStructureData(EcfFile, this, StructureLevel);
                InternalAttributes.Add(attribute);
                EcfFile?.SetUnsavedDataFlag();
                return true;
            }
            return false;
        }
        public int AddAttributes(List<EcfAttribute> attributes)
        {
            int count = 0;
            attributes?.ForEach(attribute =>
            {
                if (AddAttribute(attribute))
                {
                    count++;
                }
            });
            return count;
        }
        public void ClearAttributes()
        {
            InternalAttributes.Clear();
        }

        protected override void OnStructureDataUpdate()
        {
            UpdateDefinition(EcfFile?.Definition.BlockParameters);
            InternalAttributes.ForEach(attribute => {
                attribute.UpdateStructureData(EcfFile, this, StructureLevel);
                attribute.UpdateDefinition(EcfFile?.Definition.ParameterAttributes);
            });
        }
    }
    public class EcfBlock : EcfStructureItem
    {
        public string PreMark { get; private set; }
        public string DataType { get; private set; }
        public string PostMark { get; private set; }

        public string Id { get; private set; } = null;
        public string RefTarget { get; private set; } = null;
        public string RefSource { get; private set; } = null;
        public EcfBlock Inheritor { get; set; } = null;

        private List<EcfAttribute> InternalAttributes { get; } = new List<EcfAttribute>();
        public ReadOnlyCollection<EcfAttribute> Attributes { get; }
        private List<EcfStructureItem> InternalChildItems { get; } = new List<EcfStructureItem>();
        public ReadOnlyCollection<EcfStructureItem> ChildItems { get; }

        public EcfBlock(string preMark, string blockType, string postMark) : base("Block")
        {
            Attributes = InternalAttributes.AsReadOnly();
            ChildItems = InternalChildItems.AsReadOnly();

            PreMark = preMark;
            DataType = blockType;
            PostMark = postMark;
        }
        public EcfBlock(string preMark, string blockType, string postMark, List<EcfAttribute> attributes, List<EcfStructureItem> childItems)
            : this(preMark, blockType, postMark)
        {
            AddAttributes(attributes);
            AddChilds(childItems);
        }
        public EcfBlock(string preMark, string blockType, string postMark, List<EcfAttribute> attributes, List<EcfParameter> parameters)
            : this(preMark, blockType, postMark)
        {
            AddAttributes(attributes);
            AddChilds(parameters);
        }
        public EcfBlock(string preMark, string blockType, string postMark, List<EcfAttribute> attributes, List<EcfBlock> blocks)
            : this(preMark, blockType, postMark)
        {
            AddAttributes(attributes);
            AddChilds(blocks);
        }

        // copyconstructor
        public EcfBlock(EcfBlock template) : base(template)
        {
            Attributes = InternalAttributes.AsReadOnly();
            ChildItems = InternalChildItems.AsReadOnly();

            PreMark = template.PreMark;
            DataType = template.DataType;
            PostMark = template.PostMark;
            Id = template.Id;
            RefTarget = template.RefTarget;
            RefSource = template.RefSource;
            Inheritor = template.Inheritor;

            AddAttributes(template.Attributes.Select(attribute => new EcfAttribute(attribute)).ToList());
            List<EcfStructureItem> childs = new List<EcfStructureItem>();
            template.ChildItems.ToList().ForEach(child => {
                switch (child)
                {
                    case EcfComment comment: childs.Add(new EcfComment(comment)); return;
                    case EcfAttribute attribute: childs.Add(new EcfAttribute(attribute)); return;
                    case EcfParameter parameter: childs.Add(new EcfParameter(parameter)); return;
                    case EcfBlock block: childs.Add(new EcfBlock(block)); return;
                    default: return;
                }
            });
            AddChilds(childs);
        }

        // publics
        public void UpdateTypeData(string preMark, string blockType, string postMark)
        {
            PreMark = preMark;
            DataType = blockType;
            PostMark = postMark;
        }
        public override string ToString()
        {
            return string.Format("Block with preMark: '{0}', blockDataType: '{1}', name: '{4}', items: '{2}', attributes: '{3}'",
                PreMark, DataType, ChildItems.Count, Attributes.Count, GetAttributeFirstValue("Name"));
        }
        public override List<EcfError> GetDeepErrorList()
        {
            List<EcfError> errors = new List<EcfError>(Errors);
            errors.AddRange(Attributes.SelectMany(attribute => attribute.GetDeepErrorList()));
            errors.AddRange(ChildItems.Where(item => item is EcfStructureItem).Cast<EcfStructureItem>().SelectMany(item => item.GetDeepErrorList()));
            return errors;
        }
        public override string GetFullName()
        {
            StringBuilder name = new StringBuilder();
            EcfBlock item = this;
            while (item != null)
            {
                if (name.Length > 0)
                {
                    name.Insert(0, " / ");
                }
                name.Insert(0, item.BuildIdentification());
                item = item.Parent as EcfBlock;
            }
            return name.ToString();
        }
        public override int Revalidate()
        {
            FormatDefinition definition = EcfFile?.Definition;
            if (definition == null) { throw new InvalidOperationException("Validation is only possible for added elements"); }

            int errorCount = RevalidateDataType();
            errorCount += RevalidateParameters();
            errorCount += InternalChildItems.Sum(item => item.Revalidate());
            errorCount += RevalidateAttributes();
            errorCount += InternalAttributes.Sum(attr => attr.Revalidate());
            return errorCount;
        }
        public int RevalidateDataType()
        {
            FormatDefinition definition = EcfFile?.Definition;
            if (definition == null) { throw new InvalidOperationException("Validation is only possible with File reference"); }

            int errorCount = 0;

            RemoveErrors(EcfErrors.BlockPreMarkMissing, EcfErrors.BlockPreMarkUnknown, 
                EcfErrors.BlockDataTypeMissing, EcfErrors.BlockDataTypeUnknown,
                EcfErrors.BlockPostMarkMissing, EcfErrors.BlockPostMarkUnknown);
            
            errorCount += AddErrors(CheckBlockPreMark(PreMark, definition.BlockTypePreMarks));
            errorCount += AddErrors(CheckBlockPreMark(DataType, IsRoot() ? definition.RootBlockTypes : definition.ChildBlockTypes));
            errorCount += AddErrors(CheckBlockPostMark(PostMark, definition.BlockTypePostMarks));
            return errorCount;
        }
        public int RevalidateUniqueness(List<EcfBlock> blockList)
        {
            if (EcfFile?.Definition == null) { throw new InvalidOperationException("Validation is only possible with File reference"); }

            RemoveErrors(EcfErrors.BlockIdNotUnique);
            
            List<EcfBlock> doubledBlocks = blockList.Where(block => !block.Equals(this) &&
                ((Id?.Equals(block.Id) ?? false) || (RefTarget?.Equals(block.RefTarget) ?? false))).ToList();
            
            return AddErrors(doubledBlocks.Select(block => new EcfError(EcfErrors.BlockIdNotUnique, block.BuildIdentification())).ToList());
        }
        public bool RevalidateReferenceHighLevel(List<EcfBlock> blockList)
        {
            RemoveErrors(EcfErrors.BlockInheritorMissing);
            if (Inheritor != null && !blockList.Contains(Inheritor))
            {
                return AddError(new EcfError(EcfErrors.BlockInheritorMissing, RefSource));
            }
            return true;
        }
        public bool RevalidateReferenceRepairing(List<EcfBlock> blockList)
        {
            if (EcfFile?.Definition == null) { throw new InvalidOperationException("Validation is only possible with File reference"); }

            RemoveErrors(EcfErrors.BlockInheritorMissing);

            bool result = AddError(CheckBlockReferenceValid(this, blockList, out EcfBlock inheriter));
            Inheritor = inheriter;
            return result;
        }
        public int RevalidateParameters()
        {
            FormatDefinition definition = EcfFile?.Definition;
            if (definition == null) { throw new InvalidOperationException("Validation is only possible with File reference"); }

            RemoveErrors(EcfErrors.ParameterMissing, EcfErrors.ParameterDoubled);
            List<EcfParameter> parameters = ChildItems.Where(item => item is EcfParameter).Cast<EcfParameter>().ToList();
            return AddErrors(CheckParametersValid(parameters, definition.BlockParameters));
        }
        public int RevalidateAttributes()
        {
            FormatDefinition definition = EcfFile?.Definition;
            if (definition == null) { throw new InvalidOperationException("Validation is only possible with File reference"); }

            RemoveErrors(EcfErrors.AttributeMissing, EcfErrors.AttributeDoubled);
            return AddErrors(CheckAttributesValid(InternalAttributes, IsRoot() ? definition.RootBlockAttributes : definition.ChildBlockAttributes));
        }
        public List<T> GetDeepChildList<T>() where T : EcfBaseItem
        {
            List<T> childs = new List<T>(ChildItems.Where(child => child is T).Cast<T>());
            foreach (EcfBlock subBlock in ChildItems.Where(item => item is EcfBlock).Cast<EcfBlock>())
            {
                childs.AddRange(subBlock.GetDeepChildList<T>());
            }
            return childs;
        }
        public bool AddChild(EcfStructureItem child)
        {
            if (child != null)
            {
                child.UpdateStructureData(EcfFile, this, StructureLevel);
                InternalChildItems.Add(child);
                EcfFile?.SetUnsavedDataFlag();
                return true;
            }
            return false;
        }
        public int AddChilds(List<EcfStructureItem> childs)
        {
            int count = 0;
            childs?.ForEach(child => { 
                if (AddChild(child))
                {
                    count++;
                } 
            });
            return count;
        }
        public int AddChilds(List<EcfParameter> parameters)
        {
            int count = 0;
            parameters?.ForEach(parameter => {
                if (AddChild(parameter))
                {
                    count++;
                }
            });
            return count;
        }
        public int AddChilds(List<EcfBlock> blocks)
        {
            int count = 0;
            blocks?.ForEach(block => {
                if (AddChild(block))
                {
                    count++;
                }
            });
            return count;
        }
        public bool AddChild(EcfStructureItem child, EcfStructureItem precedingChild)
        {
            if (child != null)
            {
                child.UpdateStructureData(EcfFile, this, StructureLevel);
                int index = InternalChildItems.IndexOf(precedingChild);
                if (index < 0)
                {
                    InternalChildItems.Add(child);
                }
                else
                {
                    InternalChildItems.Insert(index + 1, child);
                }
                EcfFile?.SetUnsavedDataFlag();
                return true;
            }
            return false;
        }
        public int AddChilds(List<EcfStructureItem> childs, EcfStructureItem precedingChild)
        {
            int count = 0;
            EcfStructureItem preItem = precedingChild;
            childs?.ForEach(child =>
            {
                if (AddChild(child, preItem))
                {
                    count++;
                }
                preItem = child;
            });
            return count;
        }
        public bool AddAttribute(EcfAttribute attribute)
        {
            if (attribute != null)
            {
                attribute.UpdateStructureData(EcfFile, this, StructureLevel);
                SetIdentification(attribute);
                InternalAttributes.Add(attribute);
                EcfFile?.SetUnsavedDataFlag();
                return true;
            }
            return false;
        }
        public int AddAttributes(List<EcfAttribute> attributes)
        {
            int count = 0;
            attributes?.ForEach(attribute => 
            { 
                if (AddAttribute(attribute))
                {
                    count++;
                }
            });
            return count;
        }
        public string GetAttributeFirstValue(string attrName)
        {
            return InternalAttributes.FirstOrDefault(attr => attr.Key.Equals(attrName))?.GetFirstValue();
        }
        public bool HasAttribute(string attrName)
        {
            return InternalAttributes.Any(attr => attr.Key.Equals(attrName));
        }
        public bool HasAttributeValue(string attrValue)
        {
            return InternalAttributes.Any(attr => attr.ValueGroups.Any(group => group.Values.Any(value => value.Equals(attrValue))));
        }
        public bool HasParameter(string paramName)
        {
            return InternalChildItems.Where(item => item is EcfParameter).Cast<EcfParameter>().Any(param => param.Key.Equals(paramName));
        }
        public override string BuildIdentification()
        {
            StringBuilder identification = new StringBuilder(DataType ?? string.Empty);
            if (Id != null) {
                identification.Append(", Id: ");
                identification.Append(Id);
            }
            if (RefTarget != null)
            {
                identification.Append(", Name: ");
                identification.Append(RefTarget);
            }
            if (!IsRoot())
            {
                identification.Append(", Index: ");
                identification.Append(GetIndexInStructureLevel<EcfBlock>());
                if (Attributes.Count > 0)
                {
                    identification.Append(", ");
                    identification.Append(string.Join(", ", Attributes.Select(attr => attr.Key)));
                }

            }
            return identification.ToString();
        }
        public bool RemoveChild(EcfStructureItem childItem)
        {
            if (childItem != null)
            {
                InternalChildItems.Remove(childItem);
                EcfFile?.SetUnsavedDataFlag();
                return true;
            }
            return false;
        }
        public int RemoveChilds(List<EcfStructureItem> childItems)
        {
            int count = 0;
            childItems?.ForEach(child => {
                if (RemoveChild(child))
                {
                    count++;
                }
            });
            return count;
        }
        public int RemoveChilds(List<EcfParameter> parameters)
        {
            int count = 0;
            parameters?.ForEach(parameter => {
                if (RemoveChild(parameter))
                {
                    count++;
                }
            });
            return count;
        }
        public int RemoveChilds(List<EcfBlock> blocks)
        {
            int count = 0;
            blocks?.ForEach(block => {
                if (RemoveChild(block))
                {
                    count++;
                }
            });
            return count;
        }
        public void ClearAttributes()
        {
            InternalAttributes.Clear();
            UpdateIdentification();
        }

        // private
        private void SetIdentification(EcfAttribute attribute)
        {
            FormatDefinition definition = EcfFile?.Definition;
            if (definition != null)
            {
                if (attribute.Key.Equals(definition.BlockIdentificationAttribute))
                {
                    Id = attribute.GetFirstValue();
                }
                else if (attribute.Key.Equals(definition.BlockReferenceTargetAttribute))
                {
                    RefTarget = attribute.GetFirstValue();
                }
                else if (attribute.Key.Equals(definition.BlockReferenceSourceAttribute))
                {
                    RefSource = attribute.GetFirstValue();
                }
            }
        }
        private void UpdateIdentification()
        {
            Id = GetAttributeFirstValue(EcfFile?.Definition.BlockIdentificationAttribute);
            RefTarget = GetAttributeFirstValue(EcfFile?.Definition.BlockReferenceTargetAttribute);
            RefSource = GetAttributeFirstValue(EcfFile?.Definition.BlockReferenceSourceAttribute);
        }
        protected override void OnStructureDataUpdate()
        {
            InternalAttributes.ForEach(attribute => {
                attribute.UpdateStructureData(EcfFile, this, StructureLevel);
                attribute.UpdateDefinition(IsRoot() ? EcfFile?.Definition.RootBlockAttributes : EcfFile?.Definition.ChildBlockAttributes);
            });
            InternalChildItems.ForEach(child => child.UpdateStructureData(EcfFile, this, StructureLevel));
            UpdateIdentification();
        }
    }
    public class EcfComment : EcfStructureItem
    {
        public EcfComment(string comment) : base("Comment")
        {
            AddComment(comment);
        }
        public EcfComment(List<string> comments) : base("Comment")
        {
            AddComments(comments);
        }

        // copy constructor
        public EcfComment(EcfComment template) : base(template)
        {

        }

        public override List<EcfError> GetDeepErrorList()
        {
            return Errors.ToList();
        }
        public override string GetFullName()
        {
            StringBuilder name = new StringBuilder();
            if (!IsRoot())
            {
                name.Append(Parent.GetFullName());
                name.Append(" / ");
            }
            name.Append(DefaultName);
            return name.ToString();
        }
        public override string ToString()
        {
            return string.Format("{0}: '{1}'", DefaultName, string.Join(" / ", Comments));
        }
        public override string BuildIdentification()
        {
            return string.Format("{0} {1}", DefaultName, GetHashCode());
        }
        public override int Revalidate()
        {
            return 0;
        }

        protected override void OnStructureDataUpdate()
        {
            
        }
    }
    public class EcfValueGroup : EcfBaseItem
    {
        private List<string> InternalValues { get; } = new List<string>();
        public ReadOnlyCollection<string> Values { get; }

        public EcfValueGroup() : base()
        {
            Values = InternalValues.AsReadOnly();
        }
        public EcfValueGroup(string value) : this()
        {
            AddValue(value);
        }
        public EcfValueGroup(List<string> values) : this()
        {
            AddValues(values);
        }

        // copy constructor
        public EcfValueGroup(EcfValueGroup template) : base(template)
        {
            Values = InternalValues.AsReadOnly();
            AddValues(template.InternalValues);
        }

        public override string ToString()
        {
            return string.Format("ValueGroup with '{0}'", string.Join(" / ", InternalValues));
        }
        public bool AddValue(string value)
        {
            if (value != null) {
                InternalValues.Add(value);
                EcfFile?.SetUnsavedDataFlag();
                return true;
            }
            return false;
        }
        public int AddValues(List<string> values)
        {
            int count = 0;
            values?.ForEach(value =>
            {
                if (AddValue(value))
                {
                    count++;
                }
            });
            return count;
        }

        protected override void OnStructureDataUpdate()
        {
            
        }
    }

    // definition data structures
    public class FormatDefinition
    {
        public string FilePathAndName { get; }
        public string FileType { get; }

        public ReadOnlyCollection<string> SingleLineCommentStarts { get; }
        public ReadOnlyCollection<StringPairDefinition> MultiLineCommentPairs { get; }
        public ReadOnlyCollection<StringPairDefinition> BlockIdentifierPairs { get; }
        public ReadOnlyCollection<string> OuterTrimmingPhrases { get; }
        public ReadOnlyCollection<StringPairDefinition> EscapeIdentifiersPairs { get; }

        public string ItemSeperator { get; }
        public string ItemValueSeperator { get; }
        public string ValueSeperator { get; }
        public string ValueGroupSeperator { get; }
        public string ValueFractionalSeperator { get; }
        public string MagicSpacer { get; }
        public string BlockIdentificationAttribute { get; }
        public string BlockReferenceSourceAttribute { get; }
        public string BlockReferenceTargetAttribute { get; }

        public ReadOnlyCollection<string> ProhibitedValuePhrases { get; }

        public ReadOnlyCollection<BlockValueDefinition> BlockTypePreMarks { get; }
        public ReadOnlyCollection<BlockValueDefinition> BlockTypePostMarks { get; }
        public ReadOnlyCollection<BlockValueDefinition> RootBlockTypes { get; }
        public ReadOnlyCollection<ItemDefinition> RootBlockAttributes { get; }
        public ReadOnlyCollection<BlockValueDefinition> ChildBlockTypes { get; }
        public ReadOnlyCollection<ItemDefinition> ChildBlockAttributes { get; }
        public ReadOnlyCollection<ItemDefinition> BlockParameters { get; }
        public ReadOnlyCollection<ItemDefinition> ParameterAttributes { get; }

        public string WritingSingleLineCommentStart { get; }
        public StringPairDefinition WritingBlockIdentifierPair { get;}
        public StringPairDefinition WritingEscapeIdentifiersPair { get; }

        public FormatDefinition(string filePathAndName, string fileType,
            List<string> singleLineCommentStarts, List<StringPairDefinition> multiLineCommentPairs,
            List<StringPairDefinition> blockPairs, List<StringPairDefinition> escapeIdentifierPairs, List<string> outerTrimmingPhrases,
            string itemSeperator, string itemValueSeperator, string valueSeperator, 
            string valueGroupSeperator, string valueFractionalSeperator, string magicSpacer,
            string blockIdentificationAttribute, string blockReferenceSourceAttribute, string blockReferenceTargetAttribute,
            List<BlockValueDefinition> blockTypePreMarks, List<BlockValueDefinition> blockTypePostMarks,
            List<BlockValueDefinition> rootBlockTypes, List<ItemDefinition> rootBlockAttributes,
            List<BlockValueDefinition> childBlockTypes, List<ItemDefinition> childBlockAttributes,
            List<ItemDefinition> blockParameters, List<ItemDefinition> parameterAttributes)
        {
            FilePathAndName = filePathAndName;
            FileType = fileType;

            SingleLineCommentStarts = singleLineCommentStarts.AsReadOnly();
            MultiLineCommentPairs = multiLineCommentPairs.AsReadOnly();
            BlockIdentifierPairs = blockPairs.AsReadOnly();
            OuterTrimmingPhrases = outerTrimmingPhrases.AsReadOnly();
            EscapeIdentifiersPairs = escapeIdentifierPairs.AsReadOnly();

            ItemSeperator = itemSeperator;
            ItemValueSeperator = itemValueSeperator;
            ValueSeperator = valueSeperator;
            ValueGroupSeperator = valueGroupSeperator;
            ValueFractionalSeperator = valueFractionalSeperator;
            MagicSpacer = magicSpacer;
            BlockIdentificationAttribute = blockIdentificationAttribute;
            BlockReferenceSourceAttribute = blockReferenceSourceAttribute;
            BlockReferenceTargetAttribute = blockReferenceTargetAttribute;

            HashSet<string> prohibitedPhrases = new HashSet<string>();
            foreach (string start in SingleLineCommentStarts) { prohibitedPhrases.Add(start); }
            foreach (StringPairDefinition pair in MultiLineCommentPairs) { prohibitedPhrases.Add(pair.Opener); prohibitedPhrases.Add(pair.Closer); }
            foreach (StringPairDefinition pair in BlockIdentifierPairs) { prohibitedPhrases.Add(pair.Opener); prohibitedPhrases.Add(pair.Closer); }
            foreach (StringPairDefinition pair in EscapeIdentifiersPairs) { prohibitedPhrases.Add(pair.Opener); prohibitedPhrases.Add(pair.Closer); }
            prohibitedPhrases.Add(ItemSeperator);
            prohibitedPhrases.Add(ItemValueSeperator);
            prohibitedPhrases.Add(ValueSeperator);
            prohibitedPhrases.Add(ValueGroupSeperator);
            ProhibitedValuePhrases = prohibitedPhrases.ToList().AsReadOnly();

            BlockTypePreMarks = blockTypePreMarks.AsReadOnly();
            BlockTypePostMarks = blockTypePostMarks.AsReadOnly();
            RootBlockTypes = rootBlockTypes.AsReadOnly();
            RootBlockAttributes = rootBlockAttributes.AsReadOnly();
            ChildBlockTypes = childBlockTypes.AsReadOnly();
            ChildBlockAttributes = childBlockAttributes.AsReadOnly();
            BlockParameters = blockParameters.AsReadOnly();
            ParameterAttributes = parameterAttributes.AsReadOnly();

            WritingSingleLineCommentStart = singleLineCommentStarts.First();
            WritingBlockIdentifierPair = blockPairs.First();
            WritingEscapeIdentifiersPair = escapeIdentifierPairs.First();
        }
        public FormatDefinition(FormatDefinition template)
        {
            FilePathAndName = template.FilePathAndName;
            FileType = template.FileType;

            SingleLineCommentStarts = template.SingleLineCommentStarts.ToList().AsReadOnly();
            MultiLineCommentPairs = template.MultiLineCommentPairs.Select(pair => new StringPairDefinition(pair)).ToList().AsReadOnly();
            BlockIdentifierPairs = template.BlockIdentifierPairs.Select(pair => new StringPairDefinition(pair)).ToList().AsReadOnly();
            OuterTrimmingPhrases = template.OuterTrimmingPhrases.ToList().AsReadOnly();
            EscapeIdentifiersPairs = template.EscapeIdentifiersPairs.Select(pair => new StringPairDefinition(pair)).ToList().AsReadOnly();

            ItemSeperator = template.ItemSeperator;
            ItemValueSeperator = template.ItemValueSeperator;
            ValueSeperator = template.ValueSeperator;
            ValueGroupSeperator = template.ValueGroupSeperator;
            ValueFractionalSeperator = template.ValueFractionalSeperator;
            MagicSpacer = template.MagicSpacer;
            BlockIdentificationAttribute = template.BlockIdentificationAttribute;
            BlockReferenceSourceAttribute = template.BlockReferenceSourceAttribute;
            BlockReferenceTargetAttribute = template.BlockReferenceTargetAttribute;

            ProhibitedValuePhrases = template.ProhibitedValuePhrases.ToList().AsReadOnly();

            BlockTypePreMarks = template.BlockTypePreMarks.Select(mark => new BlockValueDefinition(mark)).ToList().AsReadOnly();
            BlockTypePostMarks = template.BlockTypePostMarks.Select(mark => new BlockValueDefinition(mark)).ToList().AsReadOnly();
            RootBlockTypes = template.RootBlockTypes.Select(type => new BlockValueDefinition(type)).ToList().AsReadOnly();
            RootBlockAttributes = template.RootBlockAttributes.Select(item => new ItemDefinition(item)).ToList().AsReadOnly();
            ChildBlockTypes = template.ChildBlockTypes.Select(type => new BlockValueDefinition(type)).ToList().AsReadOnly();
            ChildBlockAttributes = template.ChildBlockAttributes.Select(item => new ItemDefinition(item)).ToList().AsReadOnly();
            BlockParameters = template.BlockParameters.Select(item => new ItemDefinition(item)).ToList().AsReadOnly();
            ParameterAttributes = template.ParameterAttributes.Select(item => new ItemDefinition(item)).ToList().AsReadOnly();

            WritingSingleLineCommentStart = template.WritingSingleLineCommentStart;
            WritingBlockIdentifierPair = template.WritingBlockIdentifierPair;
            WritingEscapeIdentifiersPair = template.WritingEscapeIdentifiersPair;
        }
    }
    public class BlockValueDefinition
    {
        public string Value { get; }
        public bool IsOptional { get; }

        public BlockValueDefinition(string value, string isOptional, bool valueCheck)
        {
            if (valueCheck && !IsKeyValid(value)) { throw new ArgumentException(string.Format("'{0}' is not a valid 'value' parameter", value)); }
            if (!valueCheck && value == null) { throw new ArgumentException("Null is not a valid 'value' parameter"); }
            if (!bool.TryParse(isOptional, out bool optional)) { throw new ArgumentException(string.Format("'{0}' is not a valid 'isOptional' parameter", isOptional)); }
            Value = value;
            IsOptional = optional;
        }
        public BlockValueDefinition(string value, string isOptional) : this(value, isOptional, true)
        {
            
        }
        public BlockValueDefinition(BlockValueDefinition template)
        {
            Value = template.Value;
            IsOptional = template.IsOptional;
        }
    }
    public class ItemDefinition
    {
        public string Name { get; }
        public bool IsOptional { get; }
        public bool HasValue { get; }
        public bool AllowBlank { get; }
        public bool IsForceEscaped { get; }
        public string Info { get; }

        public ItemDefinition(string name, string isOptional, string hasValue, string allowBlank, string isForceEscaped, string info)
        {
            if (!IsKeyValid(name)) { throw new ArgumentException(string.Format("'{0}' is not a valid 'name' parameter", name)); }
            if (!bool.TryParse(isOptional, out bool optional)) { throw new ArgumentException(string.Format("'{0}' is not a valid 'isOptional' parameter", isOptional)); }
            if (!bool.TryParse(hasValue, out bool value)) { throw new ArgumentException(string.Format("'{0}' is not a valid 'hasValue' parameter", hasValue)); }
            if (!bool.TryParse(allowBlank, out bool blank)) { throw new ArgumentException(string.Format("'{0}' is not a valid 'canBlank' parameter", allowBlank)); }
            if (!bool.TryParse(isForceEscaped, out bool forceEscaped)) { throw new ArgumentException(string.Format("'{0}' is not a valid 'forceEscape' parameter", isForceEscaped)); }
            Name = name;
            IsOptional = optional;
            HasValue = value;
            AllowBlank = blank;
            IsForceEscaped = forceEscaped;
            Info = info ?? "";
        }
        public ItemDefinition(ItemDefinition template)
        {
            Name = template.Name;
            IsOptional = template.IsOptional;
            HasValue = template.HasValue;
            AllowBlank = template.AllowBlank;
            IsForceEscaped = template.IsForceEscaped;
            Info = template.Info;
        }

        public override string ToString()
        {
            return string.Format("ItemDefinition: {0}, info: {1}", Name, Info);
        }
    }
    public class StringPairDefinition
    {
        public string Opener { get; }
        public string Closer { get; }
        public StringPairDefinition(string opener, string closer)
        {
            if (!IsKeyValid(opener)) { throw new ArgumentException(string.Format("'{0}' is not a valid 'opener' parameter", opener)); }
            if (!IsKeyValid(closer)) { throw new ArgumentException(string.Format("'{0}' is not a valid 'closer' parameter", closer)); }
            Opener = opener;
            Closer = closer;
        }
        public StringPairDefinition(StringPairDefinition template)
        {
            Opener = template.Opener;
            Closer = template.Closer;
        }
    }
}