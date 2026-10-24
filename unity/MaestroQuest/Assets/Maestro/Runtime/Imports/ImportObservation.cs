// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Text;
using Newtonsoft.Json;
namespace Maestro.Quest.Imports
{
    /// <summary>Display metadata only. Exact asset/request/motion identities are never shortened.</summary>
    internal static class ImportObservation
    {
        internal const int MotionPageSize=8,TextJsonBudget=128;
        internal static string Text(string value)
        {
            if(string.IsNullOrEmpty(value))return "";var output=new StringBuilder();
            for(int index=0;index<value.Length&&index<128;index++){
                char current=value[index];if(current=='<'||current=='>')continue;
                string part;
                if(char.IsHighSurrogate(current)){
                    if(index+1>=value.Length||!char.IsLowSurrogate(value[index+1]))continue;
                    part=value.Substring(index++,2);
                }else if(char.IsLowSurrogate(current))continue;
                else part=char.IsControl(current)?" ":current.ToString();
                string candidate=output.ToString()+part;
                if(candidate.Length>128||JsonConvert.ToString(candidate).Length>TextJsonBudget)break;
                output.Append(part);
            }
            return output.ToString();
        }
    }
}
