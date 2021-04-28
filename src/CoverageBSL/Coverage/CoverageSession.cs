﻿using com.github.yukon39.DebugBSL;
using com.github.yukon39.DebugBSL.debugger.debugAutoAttach;
using com.github.yukon39.DebugBSL.debugger.debugBaseData;
using com.github.yukon39.DebugBSL.debugger.debugMeasure;
using com.github.yukon39.DebugBSL.debugger.debugRDBGRequestResponse;
using log4net;
using ScriptEngine;
using ScriptEngine.HostedScript.Library;
using ScriptEngine.Machine;
using ScriptEngine.Machine.Contexts;
using ScriptEngine.Machine.Values;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace com.github.yukon39.CoverageBSL.Coverage
{
    [ContextClass(typeName: "CoverageSession", typeAlias: "СессияОтладки")]
    public class CoverageSession : AutoContext<CoverageSession>, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CoverageSession));

        private readonly IDebuggerClientSession DebuggerSession;
        private readonly List<DebugTargetType> TargetTypes = DefaultTargetTypes;
        private readonly List<string> AreaNames = new List<string>();
        private readonly SemaphoreSlim CoverageSemaphore = new SemaphoreSlim(1, 1);
        private CoverageData coverageData;
        public CoverageSession(IDebuggerClient debuggerClient, string infobaseAlias)
        {
            DebuggerSession = debuggerClient.CreateSession(infobaseAlias);

            DebuggerSession.TargetStarted += HandlerTargetStartedAsync;
            DebuggerSession.TargetQuit += HandlerTargetQuitAsync;
            DebuggerSession.MeasureProcessing += HandlerMeasureProcessingAsync;
        }

        [ContextMethod("Attach", "Подключить")]
        public void Attach(string password)
        {
            try
            {
                AttachConfigureAwait(password).GetAwaiter().GetResult();
            }
            catch (RuntimeException rex)
            {
                log.Error(rex.ErrorDescription, rex);
                throw;
            }
            catch (Exception ex)
            {
                var message = Locale.NStr("en = 'Attach error';ru = 'Ошибка подключения'");
                log.Error(message, ex);
                throw new RuntimeException(message, ex);
            }
        }

        private async Task AttachConfigureAwait(string password) =>
            await AttachAsync(password).ConfigureAwait(false);

        private async Task AttachAsync(string password)
        {
            var attachResult = await DebuggerSession.AttachAsync(password.ToCharArray(), new DebuggerOptions());

            switch (attachResult)
            {
                case AttachDebugUIResult.Registered:
                    await OnSuccessfulAttachAsync();
                    break;

                case AttachDebugUIResult.CredentialsRequired:

                    throw new RuntimeException(
                        Locale.NStr("en = 'Credentials required';ru = 'Требуется указание пароля'"));

                case AttachDebugUIResult.NotRegistered:

                    throw new RuntimeException(
                        Locale.NStr("en = 'Not registered';ru = 'Не зарегистрирован'"));

                case AttachDebugUIResult.IBInDebug:

                    throw new RuntimeException(
                        Locale.NStr("en = 'IB already in debug mode';ru = 'База уже в режиме отладки'"));

                default:
                    throw new RuntimeException(
                        Locale.NStr("en = 'Unknown error';ru = 'Неизвестная ошибка'"));
            }

        }

        private async Task OnSuccessfulAttachAsync()
        {
            var data = new HTTPServerInitialDebugSettingsData();

            var autoAttachSettings = new DebugAutoAttachSettings();
            autoAttachSettings.TargetType.AddRange(TargetTypes);
            autoAttachSettings.AreaName.AddRange(AreaNames);

            await DebuggerSession.InitSettingsAsync(data);
            await DebuggerSession.ClearBreakOnNextStatementAsync();
            await DebuggerSession.SetAutoAttachSettingsAsync(autoAttachSettings);
            (await DebuggerSession.AttachedTargetsStatesAsync(""))
                .ForEach(async x => await DebuggerSession.AttachDebugTargetAsync(x.TargetID.TargetIdLight));
        }

        [ContextMethod("Detach", "Отключить")]
        public void Detach()
        {
            try
            {
                DetachConfigureAwait().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                var message = Locale.NStr("en = 'Detach error';ru = 'Ошибка отключения'");
                log.Error(message, ex);
                throw new RuntimeException(message, ex);
            }
        }

        private async Task DetachConfigureAwait() =>
            await DebuggerSession.DetachAsync().ConfigureAwait(false);

        [ContextMethod("StartPerformanceMeasure", "НачатьЗамерПроизводительности")]
        public GuidWrapper StartPerformanceMeasure()
        {
            try
            {
                return StartPerformanceMeasureConfigureAwait().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                var message = Locale.NStr("en = 'StartPerformanceMeasure error';ru = 'Ошибка начала замера производительности'");
                log.Error(message, ex);
                throw new RuntimeException(message, ex);
            }
        }

        private async Task<GuidWrapper> StartPerformanceMeasureConfigureAwait() =>
            await StartPerformanceMeasureAsync().ConfigureAwait(false);

        private async Task<GuidWrapper> StartPerformanceMeasureAsync()
        {
            var measureId = Guid.NewGuid();
            await DebuggerSession.SetMeasureModeAsync(measureId);

            await CoverageSemaphore.WaitAsync();
            coverageData = new CoverageData();
            CoverageSemaphore.Release();

            return new GuidWrapper(measureId.ToString());
        }

        [ContextMethod("StopPerformanceMeasure", "ЗавершитьЗамерПроизводительности")]
        public CoverageData StopPerformanceMeasure()
        {
            try
            {
                return StopPerformanceMeasureConfigureAwait().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                var message = Locale.NStr("en = 'StopPerformanceMeasure error';ru = 'Ошибка завершения замера производительности'");
                log.Error(message, ex);
                throw new RuntimeException(message, ex);
            }
        }

        private async Task<CoverageData> StopPerformanceMeasureConfigureAwait() =>
            await StopPerformanceMeasureAsync().ConfigureAwait(false);

        private async Task<CoverageData> StopPerformanceMeasureAsync()
        {
            await DebuggerSession.SetMeasureModeAsync(Guid.Empty);
            await DebuggerSession.PingAsync();

            await CoverageSemaphore.WaitAsync();
            var result = coverageData;
            coverageData = new CoverageData();
            CoverageSemaphore.Release();

            return result;
        }

        private async Task HandlerTargetStartedAsync(DebugTargetId targetID)
        {
            try
            {
                await DebuggerSession.AttachDebugTargetAsync(targetID.TargetIdLight);
            }
            catch (Exception ex)
            {
                var message = Locale.NStr("en = 'TargetStarted event handler error';ru = 'Ошибка обработки события TargetStarted'");
                log.Error(message, ex);
            }
        }

        private async Task HandlerTargetQuitAsync(DebugTargetId targetID)
        {
            try
            {
                await DebuggerSession.DetachDebugTargetAsync(targetID.TargetIdLight);
            }
            catch (Exception ex)
            {
                var message = Locale.NStr("en = 'TargetQuit event handler error';ru = 'Ошибка обработки события TargetQuit'");
                log.Error(message, ex);
            }
        }

        private async Task HandlerMeasureProcessingAsync(PerformanceInfoMain performanceInfo)
        {
            try
            {
                await CoverageSemaphore.WaitAsync();
                coverageData.TotalDurability += performanceInfo.TotalDurability;
                performanceInfo.ModuleData.ForEach(x => ProcessPerformanceInfoModule(x));
                CoverageSemaphore.Release();
            }
            catch (Exception ex)
            {
                var message = Locale.NStr(
                    "en = 'MeasureProcessing event handler error';" +
                    "ru = 'Ошибка обработки события MeasureProcessing'");
                log.Error(message, ex);
            }
        }

        private void ProcessPerformanceInfoModule(PerformanceInfoModule module)
        {
            var moduleBSL = new CoverageModuleId(module.ModuleID);
            var linesCoverage = coverageData.Data.Retrieve(moduleBSL) as MapImpl;
            if (linesCoverage == null)
            {
                linesCoverage = new MapImpl();
                coverageData.Data.Insert(moduleBSL, linesCoverage);
            }

            module.LineInfo.ForEach(x => ProcessPerformanceInfoLine(x, linesCoverage));
        }

        private void ProcessPerformanceInfoLine(PerformanceInfoLine line, MapImpl lineslinesCoverage) =>
            lineslinesCoverage.Insert(NumberValue.Create(line.LineNo), BooleanValue.True);

        private static List<DebugTargetType> DefaultTargetTypes => new List<DebugTargetType>()
        {
            DebugTargetType.Client,
            DebugTargetType.ManagedClient,
            DebugTargetType.WEBClient,
            DebugTargetType.Server,
            DebugTargetType.ServerEmulation
        };

        public void Dispose()
        {
            CoverageSemaphore.Dispose();
        }
    }
}
