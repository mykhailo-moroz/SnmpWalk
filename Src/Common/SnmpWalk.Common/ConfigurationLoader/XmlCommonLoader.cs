﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using SnmpWalk.Common.DataModel.Snmp;

namespace SnmpWalk.Common.ConfigurationLoader
{
    internal class XmlCommonLoader
    {
        private const string ConfMain = "oids_common";
        private const string OidFileIdentifier = "oids_";
        private const string CodesFileIdentifier = "codes_";
        private const string ConfDir = "conf";
        private const string CodesDir = "codes";
        private const string OidAttr = "oid";
        private const string RootNodename = "oid-tree";
        private const string DecimalAttr = "Decimal";
        private const string NameAttr = "Name";
        private const string DescAttr = "Description";

        private List<FileInfo> _commoInfos;
        private List<FileInfo> _codesInfo;

        private readonly string _currentDir = Directory.GetCurrentDirectory();
        private static readonly Lazy<XmlCommonLoader> CommonInstance = new Lazy<XmlCommonLoader>(() => new XmlCommonLoader());
        private readonly List<Oid> _oids = new List<Oid>();

        public XmlCommonLoader Instance
        {
            get
            {
                Initialize();
                return CommonInstance.Value;
            }
        }

        public List<Oid> Oids
        {
            get { return _oids; }
        }

        private void Initialize()
        {
            var confPath = Path.Combine(_currentDir, ConfDir);
            var codesPath = Path.Combine(_currentDir, ConfDir, CodesDir);

            if (!Directory.Exists(confPath) && !Directory.Exists(codesPath)) return;

            var dirInfo = new DirectoryInfo(confPath);
            _commoInfos = dirInfo.GetFiles("*.xml").Where(file => file.Name.Contains(OidFileIdentifier)).ToList();

            var codesDirInfo = new DirectoryInfo(codesPath);
            _codesInfo = codesDirInfo.GetFiles("*.xml").Where(file => file.Name.Contains(CodesFileIdentifier)).ToList();

            if (!_commoInfos.Any() || _commoInfos.All(file => file.Name != ConfMain)) return;

            foreach (var file in _commoInfos)
            {
                var xml = XDocument.Load(file.OpenRead());

                if (xml.Root == null) continue;
                var rootNode = xml.Root;

                if (!ValidateOidFile(rootNode)) continue;

                var subNode = (XElement)rootNode.FirstNode;

                if (string.IsNullOrEmpty(subNode.FirstAttribute.Name.LocalName) || subNode.FirstAttribute.Name.LocalName != OidAttr) continue;

                var rootOid = new Oid(subNode.FirstAttribute.Value, subNode.Name.LocalName, subNode.Name.LocalName);

                var oids = subNode.Elements();

                var childOids = oids.Select(oid => new Oid(oid.FirstAttribute.Value, oid.Name.LocalName, string.Concat(rootOid.Name, ".", oid.Name.LocalName))).ToList();

                rootOid.ChildOids = InitializeCodes(childOids);

                _oids.Add(rootOid);
            }
        }

        private static bool ValidateOidFile(XElement rootNode)
        {
            return rootNode.Name.LocalName.Equals(RootNodename);
        }

        private bool ValidateCodeFile(XElement rootNode)
        {
            return rootNode.Name.LocalName.Equals(CodesDir);
        }

        private List<Oid> InitializeCodes(List<Oid> oids)
        {
            for (var i = 0; i < oids.Count; i++)
            {
                if (_codesInfo.Any(file => file.Name.Contains(oids[i].Name)))
                {
                    oids[i] = InitializeCode(oids[i], _codesInfo.First(file => file.Name.Contains(oids[i].Name)));
                }
            }

            return oids;
        }

        private Oid InitializeCode(Oid oid, FileInfo file)
        {
            var childoids = new List<Oid>();
            var xml = XDocument.Load(file.OpenRead());

            if (xml.Root == null) return oid;
            var rootNode = xml.Root;

            if (!ValidateCodeFile(rootNode)) return oid;

            var codesElements = rootNode.Elements();

            foreach (var element in codesElements)
            {
                var decimalElement = element.Element(DecimalAttr);
                string objId = null;
                string name = null;
                string fullName = null;
                string decsr = null;

                if (decimalElement != null)
                {
                    var decVal = decimalElement.Value;
                    objId = CreateOid(oid.Value, decVal);
                }

                var nameElement = element.Element(NameAttr);

                if (nameElement != null)
                {
                    name = nameElement.Value;
                    fullName = CreateOid(oid.Name, name);
                }

                var descriptionElement = element.Element(DescAttr);

                if (descriptionElement != null)
                {
                    decsr = descriptionElement.Value;
                }

                childoids.Add(new Oid(objId, name, fullName) { Description = decsr });
            }

            if (childoids.Any())
            {
                oid.ChildOids = InitializeCodes(childoids);
            }

            return oid;
        }

        private string CreateOid(string oid, string index)
        {
            return string.Concat(oid, ".", index);
        }
    }
}
