using System;
using UnityEditor;
using UnityEngine;
namespace Abandoned.SpaceApps.EditorTools
{
    public static class SurveyorChapterWebBuild
    {
        public static void BuildFromCommandLine()
        {
            try { SurveyorChapterBuilder.BuildMenu();SurveyorDocumentaryAudit.Write();WebBaselineBuilder.Build(new[]{SurveyorArchiveBuilder.Scene,SurveyorChapterBuilder.Scene},"Builds/SurveyorWeb","QA/SUR-018");EditorApplication.Exit(0); }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
