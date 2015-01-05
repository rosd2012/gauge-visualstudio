﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using EnvDTE;
using main;
using Microsoft.VisualStudio.Text;

namespace Gauge.VisualStudio.Models
{
    public class Step
    {
        private static IList<ProtoStepValue> _allSteps;
        private static IEnumerable<GaugeImplementation> _gaugeImplementations;

        public static IEnumerable<string> GetAll()
        {
            return GetAllSteps().Select(x => x.ParameterizedStepValue);
        }

        public static string GetParsedStepValue(ITextSnapshotLine input)
        {
            var stepValueFromInput = GetStepValueFromInput(GetStepText(input));
            return GetAllSteps(true).First(value => value.StepValue == stepValueFromInput)
                   .ParameterizedStepValue;
        }

        public static CodeFunction GetStepImplementation(ITextSnapshotLine line, Project containingProject = null)
        {
            if (containingProject==null)
            {
                containingProject = GaugeDTEProvider.DTE.ActiveDocument.ProjectItem.ContainingProject;
            }

            var lineText = GetStepText(line);

            _gaugeImplementations = _gaugeImplementations ?? GaugeProject.GetGaugeImplementations(containingProject);
            var gaugeImplementation = _gaugeImplementations.FirstOrDefault(implementation => implementation.ContainsFor(lineText));
            return gaugeImplementation == null ? null : gaugeImplementation.Function;
        }


        public static string GetStepText(ITextSnapshotLine line)
        {
            var originalText = line.GetText();
            var tableRegex = new Regex(@"[ ]*\|[\w ]+\|", RegexOptions.Compiled);
            var lineText = originalText.Replace('*', ' ').Trim();
            var nextLineText = NextLineText(line);

            //if next line is a table then change the last word of the step to take in a special param
            if (tableRegex.IsMatch(nextLineText))
                lineText = string.Format("{0} {{}}", lineText);
            return lineText;
        }

        public static void Refresh()
        {
            try
            {
                _allSteps = GetAllStepsFromGauge();
                _gaugeImplementations = GaugeProject.GetGaugeImplementations(GaugeDTEProvider.DTE.ActiveDocument.ProjectItem.ContainingProject);

            }
            catch (COMException)
            {
                // happens when project closes, and saves file on close. Ignore the refresh.
            }
        }

        private static IList<ProtoStepValue> GetAllStepsFromGauge()
        {
            var gaugeApiConnection = GaugeDTEProvider.GetApiConnectionForActiveDocument();
            var stepsRequest = GetAllStepsRequest.DefaultInstance;
            var apiMessage = APIMessage.CreateBuilder()
                .SetMessageId(GenerateMessageId())
                .SetMessageType(APIMessage.Types.APIMessageType.GetAllStepsRequest)
                .SetAllStepsRequest(stepsRequest)
                .Build();

            var bytes = gaugeApiConnection.WriteAndReadApiMessage(apiMessage);
            return bytes.AllStepsResponse.AllStepsList;
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

        private static long GenerateMessageId()
        {
            return DateTime.Now.Ticks/TimeSpan.TicksPerMillisecond;
        }

        internal static string GetStepValueFromInput(string input)
        {
            var stepRegex = new Regex(@"""([^""]*)""|\<([^\>]*)\>", RegexOptions.Compiled);
            return stepRegex.Replace(input, "{}");
        }

        private static IEnumerable<ProtoStepValue> GetAllSteps(bool forceCacheUpdate=false)
        {
            if (forceCacheUpdate)
            {
                _allSteps = GetAllStepsFromGauge();
            }
            _allSteps = _allSteps ?? GetAllStepsFromGauge();
            return _allSteps;
        }
    }
}