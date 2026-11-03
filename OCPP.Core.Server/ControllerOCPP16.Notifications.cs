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
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using OCPP.Core.Server.Messages_OCPP16;

namespace OCPP.Core.Server
{
    public partial class ControllerOCPP16
    {
        /// <summary>
        /// Informational notifications that only need to be acknowledged (all have an empty response):
        /// SecurityEventNotification, FirmwareStatusNotification, DiagnosticsStatusNotification, LogStatusNotification.
        /// Previously answered with NotSupported, so the content (e.g. "StartupOfTheDevice" after a reboot) was
        /// only visible as raw JSON.
        /// </summary>
        public string HandleNotification(OCPPMessage msgIn, OCPPMessage msgOut)
        {
            string errorCode = null;
            string logText = null;

            try
            {
                Logger.LogTrace("Processing {0}...", msgIn.Action);
                JObject payload = JObject.Parse(msgIn.JsonPayload);

                switch (msgIn.Action)
                {
                    case "SecurityEventNotification":
                        logText = JoinLogParts(
                            LogPart("Type", payload.Value<string>("type")),
                            LogPart("TechInfo", payload.Value<string>("techInfo")),
                            LogPart("Timestamp", payload.Value<string>("timestamp")));
                        break;
                    case "LogStatusNotification":
                        logText = JoinLogParts(
                            LogPart("Status", payload.Value<string>("status")),
                            LogPart("RequestId", payload.Value<string>("requestId")));
                        break;
                    default:
                        // FirmwareStatusNotification / DiagnosticsStatusNotification
                        logText = LogPart("Status", payload.Value<string>("status"));
                        break;
                }

                Logger.LogInformation("{0} => ChargePoint={1} / {2}", msgIn.Action, ChargePointStatus?.Id, logText);
                msgOut.JsonPayload = "{}";
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "{0} => Exception: {1}", msgIn.Action, exp.Message);
                errorCode = ErrorCodes.FormationViolation;
                logText = msgIn.JsonPayload;
            }

            WriteMessageLog(ChargePointStatus?.Id, null, msgIn.Action, logText, errorCode);
            return errorCode;
        }
    }
}
