using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>
    /// The touchdown plan must follow the craft down the slope. Documentary scenes built before the tracking rig existed still carry
    /// a fixed-camera plan; this swaps only that plan at runtime so an old prefab never leaves the camera waiting for the craft.
    /// </summary>
    public static class SurveyorChaseShot
    {
        public const string RigId = "sur.chase", AimId = "sur.chase.look", ShotSuffix = "descent.contacts";
        /// <summary>Returns the same track when it already tracks the craft, otherwise a copy whose touchdown plan does.</summary>
        public static SequenceDefinition EnsureTracking(SequenceDefinition track)
        {
            var shots = new List<ShotDefinition>(track.Shots); bool replaced = false;
            for (int i = 0; i < shots.Count; i++)
            {
                var shot = shots[i];
                if (shot == null || !shot.ShotId.EndsWith(ShotSuffix) || shot.RigId == RigId) continue;
                shots[i] = Tracking(shot.ShotId, shot.DurationSeconds); replaced = true;
            }
            return replaced ? SequenceDefinition.Create(track.SequenceId, shots, track.Cues, track.ActorMarks) : track;
        }
        /// <summary>Camera 13-17 m to the side of the craft's ground track, easing closer as it comes to rest; aim at the craft.</summary>
        public static ShotDefinition Tracking(string shotId, float seconds) => new ShotDefinition(shotId, seconds, RigId, new[]
        {
            new ShotKeyframe(0f, new Vector3(-2f, 3.2f, -17f), Vector3.zero, 48f),
            new ShotKeyframe(seconds, new Vector3(2.5f, 2.6f, -13f), Vector3.zero, 48f),
        }, AimId, minGroundClearanceMeters: .18f);
    }
}
