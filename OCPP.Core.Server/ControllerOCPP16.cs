/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2021 dallmann consulting GmbH.
 * All Rights Reserved.
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OCPP.Core.Database;
using OCPP.Core.Server.Messages_OCPP16;

namespace OCPP.Core.Server
{
    public partial class ControllerOCPP16 : ControllerBase
    {
        /// <summary>
        /// Internal string for OCPP protocol version
        /// </summary>
        protected override string ProtocolVersion
        {
            get { return "16"; }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public ControllerOCPP16(IConfiguration config, ILoggerFactory loggerFactory, ChargePointStatus chargePointStatus, OCPPCoreContext dbContext) :
            base(config, loggerFactory, chargePointStatus, dbContext)
        {
            Logger = loggerFactory.CreateLogger(typeof(ControllerOCPP16));
        }

        /// <summary>
        /// Processes the charge point message and returns the answer message
        /// </summary>
        public OCPPMessage ProcessRequest(OCPPMessage msgIn, OCPPMiddleware ocppMiddleware)
        {
            OCPPMessage msgOut = new OCPPMessage();
            msgOut.MessageType = "3";
            msgOut.UniqueId = msgIn.UniqueId;

            string errorCode = null;

            switch (msgIn.Action)
            {
                case "BootNotification":
                    errorCode = HandleBootNotification(msgIn, msgOut);
                    break;
                case "Heartbeat":
                    errorCode = HandleHeartBeat(msgIn, msgOut);
                    break;
                case "Authorize":
                    errorCode = HandleAuthorize(msgIn, msgOut, ocppMiddleware);
                    break;
                case "StartTransaction":
                    errorCode = HandleStartTransaction(msgIn, msgOut, ocppMiddleware);
                    break;
                case "StopTransaction":
                    errorCode = HandleStopTransaction(msgIn, msgOut, ocppMiddleware);
                    break;
                case "MeterValues":
                    errorCode = HandleMeterValues(msgIn, msgOut);
                    break;
                case "StatusNotification":
                    errorCode = HandleStatusNotification(msgIn, msgOut);
                    break;
                case "DataTransfer":
                    errorCode = HandleDataTransfer(msgIn, msgOut);
                    break;
                case "SecurityEventNotification":
                case "FirmwareStatusNotification":
                case "DiagnosticsStatusNotification":
                case "LogStatusNotification":
                    errorCode = HandleNotification(msgIn, msgOut);
                    break;
                default:
                    errorCode = ErrorCodes.NotSupported;
                    WriteMessageLog(ChargePointStatus.Id, null, msgIn.Action, msgIn.JsonPayload, errorCode);
                    break;
            }

            if (!string.IsNullOrEmpty(errorCode))
            {
                // Inavlid message type => return type "4" (CALLERROR)
                msgOut.MessageType = "4";
                msgOut.ErrorCode = errorCode;
                Logger.LogDebug("ControllerOCPP16 => Return error code messge: ErrorCode={0}", errorCode);
            }

            return msgOut;
        }


        /// <summary>
        /// Processes the charge point message and returns the answer message
        /// </summary>
        public void ProcessAnswer(OCPPMessage msgIn, OCPPMessage msgOut)
        {
            // The response (msgIn) has no action => check action in original request (msgOut)
            switch (msgOut.Action)
            {
                case "Reset":
                    HandleReset(msgIn, msgOut);
                    break;
                case "UnlockConnector":
                    HandleUnlockConnector(msgIn, msgOut);
                    break;
                case "SetChargingProfile":
                    HandleSetChargingProfile(msgIn, msgOut);
                    break;
                case "ClearChargingProfile":
                    HandleClearChargingProfile(msgIn, msgOut);
                    break;
                case "GetConfiguration":
                    HandleGetConfiguration(msgIn, msgOut);
                    break;
                case "ChangeConfiguration":
                    HandleChangeConfiguration(msgIn, msgOut);
                    break;
                case "TriggerMessage":
                    HandleTriggerMessage(msgIn, msgOut);
                    break;
                case "RemoteStartTransaction":
                    HandleRemoteStartTransaction(msgIn, msgOut);
                    break;
                case "RemoteStopTransaction":
                    HandleRemoteStopTransaction(msgIn, msgOut);
                    break;
                default:
                    WriteMessageLog(ChargePointStatus.Id, null, msgOut.Action ?? msgIn.Action, msgIn.JsonPayload, "Unknown answer");
                    break;
            }
        }

        /// <summary>
        /// Processes a CALLERROR answer from the charge point to one of our requests
        /// (the regular answer handlers expect a CALLRESULT payload and can't use it)
        /// </summary>
        public void ProcessCallError(OCPPMessage msgIn, OCPPMessage msgOut)
        {
            Logger.LogWarning("CALLERROR answer: ChargePointId={0} / Action={1} / ErrCode={2} / Description={3} / Details={4}",
                ChargePointStatus?.Id, msgOut.Action, msgIn.ErrorCode, msgIn.ErrorDescription, msgIn.JsonPayload);

            WriteMessageLog(ChargePointStatus?.Id, GetRequestConnectorId(msgOut), msgOut.Action,
                JoinLogParts($"CALLERROR={msgIn.ErrorDescription}", RequestSummary(msgOut), NonEmptyJson("Details", msgIn.JsonPayload)),
                msgIn.ErrorCode);

            // Release a waiting API caller instead of letting it run into its timeout
            msgOut.TaskCompletionSource?.TrySetResult("{\"status\": \"Error\", \"errorCode\": " + JsonConvert.ToString(msgIn.ErrorCode) +
                ", \"errorDescription\": " + JsonConvert.ToString(msgIn.ErrorDescription ?? string.Empty) + "}");
        }

        /// <summary>
        /// Writes a websocket lifecycle entry (Connected / Disconnected / ...) for this charge point
        /// </summary>
        internal void WriteConnectionLog(string message, string result, string errorCode)
        {
            WriteMessageLog(ChargePointStatus?.Id, null, message, result, errorCode);
        }

        /// <summary>
        /// Connector of an outgoing request (for log entries of its answer), if the request has one
        /// </summary>
        private int? GetRequestConnectorId(OCPPMessage msgOut)
        {
            try
            {
                if (string.IsNullOrEmpty(msgOut?.JsonPayload)) return null;
                JObject req = JObject.Parse(msgOut.JsonPayload);
                int? connectorId = req.Value<int?>("connectorId");
                if (!connectorId.HasValue && req.Value<int?>("transactionId") is int txId)
                {
                    connectorId = DbContext.Find<Transaction>(txId)?.ConnectorId;
                }
                return connectorId;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Short readable summary of what we asked the charge point (for answer log entries)
        /// </summary>
        private static string RequestSummary(OCPPMessage msgOut)
        {
            try
            {
                if (string.IsNullOrEmpty(msgOut?.JsonPayload)) return null;
                JObject req = JObject.Parse(msgOut.JsonPayload);
                // ChangeConfiguration of e.g. "AuthorizationKey" must not put the secret into the log
                string configKey = req.Value<string>("key");
                bool maskValue = configKey != null &&
                    (configKey.IndexOf("key", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     configKey.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     configKey.IndexOf("secret", StringComparison.OrdinalIgnoreCase) >= 0);

                List<string> parts = new List<string>();
                foreach (JProperty prop in req.Properties())
                {
                    // Nested objects (e.g. charging profiles) are too verbose for the log line
                    if (prop.Value.Type == JTokenType.Object || prop.Value.Type == JTokenType.Array) continue;
                    string value = maskValue && prop.Name == "value" ? "***" : prop.Value.ToString();
                    parts.Add($"{char.ToUpperInvariant(prop.Name[0])}{prop.Name.Substring(1)}={value}");
                }
                return parts.Count > 0 ? string.Join(" / ", parts) : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Status of the connector as currently known in memory (for diagnosing rejected commands)
        /// </summary>
        private string KnownConnectorState(int? connectorId)
        {
            if (connectorId.HasValue &&
                ChargePointStatus?.OnlineConnectors != null &&
                ChargePointStatus.OnlineConnectors.TryGetValue(connectorId.Value, out OnlineConnectorStatus ocs))
            {
                return ocs.Status.ToString();
            }
            return null;
        }

        /// <summary>
        /// Joins the non-empty parts of a log line with " / "
        /// </summary>
        private static string JoinLogParts(params string[] parts)
        {
            return string.Join(" / ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        }

        /// <summary>
        /// "Name=Value" if the value is set, otherwise null (=> skipped by JoinLogParts)
        /// </summary>
        private static string LogPart(string name, object value)
        {
            string s = value is double d ? FormatLogNumber(d) : value is Enum e ? OcppEnumName(e) : value?.ToString();
            return string.IsNullOrWhiteSpace(s) ? null : $"{name}={s}";
        }

        private static string NonEmptyJson(string name, string json)
        {
            return string.IsNullOrWhiteSpace(json) || json.Trim() == "{}" ? null : $"{name}={json}";
        }

        /// <summary>
        /// Compact invariant number format for log text (avoids "39.530449999999995")
        /// </summary>
        private static string FormatLogNumber(double value)
        {
            return Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// OCPP spelling of a generated enum value ("Current.Import" instead of "Current_Import")
        /// </summary>
        private static string OcppEnumName(Enum value)
        {
            if (value == null) return null;
            string name = value.ToString();
            EnumMemberAttribute attr = value.GetType().GetField(name)?.GetCustomAttribute<EnumMemberAttribute>();
            return attr?.Value ?? name;
        }

        /// <summary>
        /// Helper function for writing a log entry in database
        /// </summary>
        private void WriteMessageLog(string chargePointId, int? connectorId, string message, string result, string errorCode)
        {
            try
            {
                int dbMessageLog = Configuration.GetValue<int>("DbMessageLog", 0);
                if (dbMessageLog > 0 && !string.IsNullOrWhiteSpace(chargePointId))
                {
                    bool doLog = (dbMessageLog > 1 ||
                                    (message != "BootNotification" &&
                                     message != "Heartbeat" &&
                                     message != "DataTransfer" &&
                                     message != "StatusNotification"));

                    if (doLog)
                    {
                        MessageLog msgLog = new MessageLog();
                        msgLog.ChargePointId = chargePointId;
                        msgLog.ConnectorId = connectorId;
                        msgLog.LogTime = DateTime.UtcNow;
                        msgLog.Message = message;
                        msgLog.Result = result;
                        msgLog.ErrorCode = errorCode;
                        DbContext.MessageLogs.Add(msgLog);
                        Logger.LogTrace("MessageLog => Writing entry '{0}'", message);
                        DbContext.SaveChanges();
                        /*
                         * Problem with async operation and ID generation (conflict with EF tracking)
                        _ = DbContext.SaveChangesAsync().ContinueWith(task =>
                        {
                            if (task.IsFaulted && task.Exception != null)
                            {
                                foreach (var exp in task.Exception.InnerExceptions)
                                {
                                    Logger.LogError(exp, "ControllerOCPP16.WriteMessageLog=> Error writing message async to DB: '{0}'", message);
                                }
                            }
                        });
                        */
                    }
                }
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "MessageLog => Error writing entry '{0}'", message);
            }
        }
    }
}
