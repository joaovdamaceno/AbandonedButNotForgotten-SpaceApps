using System;
using UnityEditor;
using UnityEngine;
namespace Abandoned.SpaceApps.EditorTools
{
    public static class SurveyorChapterWebBuild
    {
        public static void BuildFromCommandLine()
        {
            try { SurveyorChapterBuilder.BuildMenu();SurveyorDocumentaryAudit.Write("QA/SUR-020");WebBaselineBuilder.Build(new[]{SurveyorArchiveBuilder.Scene,SurveyorChapterBuilder.Scene},"Builds/SurveyorWeb","QA/SUR-020");EditorApplication.Exit(0); }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
