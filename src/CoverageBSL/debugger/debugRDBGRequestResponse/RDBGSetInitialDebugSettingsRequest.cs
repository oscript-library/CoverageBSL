﻿using System.Xml.Serialization;

namespace com.github.yukon39.CoverageBSL.debugger.debugRDBGRequestResponse
{
    [XmlRoot(ElementName = "request", Namespace = "http://v8.1c.ru/8.3/debugger/debugBaseData")]
    [XmlType(Namespace = "http://v8.1c.ru/8.3/debugger/debugRDBGRequestResponse")]
    public class RDBGSetInitialDebugSettingsRequest : RDbgBaseRequest, IRDBGRequest
    {
        [XmlElement(ElementName = "data")]
        public HTTPServerInitialDebugSettingsData Data;
    }
}
