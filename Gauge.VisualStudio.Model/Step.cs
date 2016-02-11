﻿// Copyright [2014, 2015] [ThoughtWorks Inc.](www.thoughtworks.com)
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Gauge.Messages;
using Gauge.VisualStudio.Core.Exceptions;
using Gauge.VisualStudio.Core.Helpers;
using Microsoft.VisualStudio.Text;

namespace Gauge.VisualStudio.Model
{
    public class Step
    {
        private readonly EnvDTE.Project _project;

        public Step(EnvDTE.Project project)
        {
            _project = project;
        }

        public IEnumerable<string> GetAll(EnvDTE.Project project)
        {
            return GetAllStepsFromGauge(project).Select(x => x.ParameterizedStepValue);
        }

        public string GetParameterizedStepValue(EnvDTE.Project project, ITextSnapshotLine input)
        {
            var stepValueFromInput = GetStepValueFromInput(project, GetStepText(input));
            return stepValueFromInput == null ? string.Empty : stepValueFromInput.ParameterizedStepValue;
        }

        public Step Parse(EnvDTE.Project project, ITextSnapshotLine input)
        {
            var stepValueFromInput = GetStepValueFromInput(project, GetStepText(input));
            

            if (stepValueFromInput == null) return default(Step);

            return new Step(_project)
            {
                Text = stepValueFromInput.ParameterizedStepValue,
                Parameters = stepValueFromInput.ParametersList.ToList()
            };
        }

        public string Text { get; set; }

        public List<string> Parameters { get; set; }

        public static string GetStepText(ITextSnapshotLine line)
        {
            var originalText = line.GetText();
            var lineText = originalText.Replace('*', ' ').Trim();

            //if next line is a table then change the last word of the step to take in a special param
            if (HasTable(line))
                lineText = string.Format("{0} <table>", lineText);
            return lineText;
        }

        public static bool HasTable(ITextSnapshotLine line)
        {
            var nextLineText = NextLineText(line);
            var tableRegex = new Regex(@"[ ]*\|[\w ]+\|", RegexOptions.Compiled);
            return tableRegex.IsMatch(nextLineText);
        }

        private static IEnumerable<ProtoStepValue> GetAllStepsFromGauge(EnvDTE.Project project)
        {
            try
            {
                var gaugeApiConnection = GaugeDaemonHelper.GetApiConnectionFor(project);
                var stepsRequest = GetAllStepsRequest.DefaultInstance;
                var apiMessage = APIMessage.CreateBuilder()
                    .SetMessageId(GenerateMessageId())
                    .SetMessageType(APIMessage.Types.APIMessageType.GetAllStepsRequest)
                    .SetAllStepsRequest(stepsRequest)
                    .Build();

                var bytes = gaugeApiConnection.WriteAndReadApiMessage(apiMessage);
                return bytes.AllStepsResponse.AllStepsList;

            }
            catch (GaugeApiInitializationException)
            {
                return Enumerable.Empty<ProtoStepValue>();
            }
        }

        private static string NextLineText(ITextSnapshotLine currentLine)
        {
            ITextSnapshotLine nextLine;
            string nextLineText;
            try
            {
                nextLine = currentLine.Snapshot.GetLineFromLineNumber(currentLine.LineNumber + 1);
                nextLineText = nextLine.GetText();
            }
            catch
            {
                return string.Empty;
            }
            return nextLineText.Trim() == string.Empty && currentLine.LineNumber < currentLine.Snapshot.LineCount ? NextLineText(nextLine) : nextLineText;
        }

        public static long GenerateMessageId()
        {
            return DateTime.Now.Ticks/TimeSpan.TicksPerMillisecond;
        }

        internal static string GetParsedStepValueFromInput(EnvDTE.Project project, string input)
        {
            var stepValueFromInput = GetStepValueFromInput(project, input);
            return stepValueFromInput == null ? string.Empty : stepValueFromInput.StepValue;
        }

        public static string GetFindRegex(EnvDTE.Project project, string input)
        {
            var parsedValue = GetParsedStepValueFromInput(project, input);
            parsedValue = parsedValue.Replace("* ", "");
            return parsedValue.Replace("{}", "(.)*");
        }

        private static ProtoStepValue GetStepValueFromInput(EnvDTE.Project project, string input)
        {
            try
            {
                var gaugeApiConnection = GaugeDaemonHelper.GetApiConnectionFor(project);
                var stepsRequest = GetStepValueRequest.CreateBuilder().SetStepText(input).Build();
                var apiMessage = APIMessage.CreateBuilder()
                    .SetMessageId(GenerateMessageId())
                    .SetMessageType(APIMessage.Types.APIMessageType.GetStepValueRequest)
                    .SetStepValueRequest(stepsRequest)
                    .Build();

                var bytes = gaugeApiConnection.WriteAndReadApiMessage(apiMessage);
                return bytes.StepValueResponse.StepValue;
            }
            catch (GaugeApiInitializationException)
            {
                return default(ProtoStepValue);
            }
        }
    }
}