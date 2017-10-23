﻿using System.Collections.Generic;
using SonicRealms.Core.Actors;
using SonicRealms.Core.Internal;
using SonicRealms.Core.Moves;
using SonicRealms.Core.Triggers;
using SonicRealms.Core.Utils;
using UnityEngine;

namespace SonicRealms.Level.Objects
{
    /// <summary>
    /// Moves the controller along a path when activated.
    /// </summary>
    public class Pipe : ReactiveEffect
    {
        public enum UpdateType
        {
            IsStatic,
            TransformChanges,
            PathOrTransformChanges
        }

        /// <summary>
        /// How fast the controller moves through the pipe, in units per second.
        /// </summary>
        [Tooltip("How fast the controller moves through the pipe, in units per second.")]
        public float TravelSpeed;

        /// <summary>
        /// How fast the controller exits the pipe, in units per second.
        /// </summary>
        [Tooltip("How fast the controller exits the pipe, in units per second.")]
        public float ExitSpeed;

        /// <summary>
        /// An object trigger that, when activated, starts the controller on the path.
        /// </summary>
        [Tooltip("An object trigger that, when activated, starts the controller on the path.")]
        public EffectTrigger EntryPoint;

        /// <summary>
        /// The path as defined by the shape of the given collider.
        /// </summary>
        [Tooltip("The path as defined by the shape of the given collider.")]
        public Collider2D Path;

        /// <summary>
        /// Indicates how the pipe's path changes in-game, if it does at all.
        /// </summary>
        [Tooltip("Indicates how the pipe's path changes in-game, if it does at all.")]
        public UpdateType UpdateMode;

        protected List<HedgehogController> Controllers;
        protected List<float> ControllerProgress;
        protected List<Vector2> Velocities; 

        private Collider2D _previousPath;
        private Vector2[] _cachedPath;
        private float _cachedLength;

        public override void Reset()
        {
            base.Reset();
            TravelSpeed = 4.2f;
            ExitSpeed = 4.2f;
            Path = GetComponentInChildren<Collider2D>();
            UpdateMode = UpdateType.IsStatic;
        }

        public override void Awake()
        {
            base.Awake();

            Controllers = new List<HedgehogController>();
            ControllerProgress = new List<float>();
            Velocities = new List<Vector2>();
        }

        public override void Start()
        {
            base.Start();

            EffectTrigger.TriggerFromChildren = false;
            EntryPoint.OnActivate.AddListener(EffectTrigger.Activate);
            ReconstructPath();
        }

        public void Update()
        {
            if (UpdateMode == UpdateType.IsStatic)
                return;

            var hasChanged = Path.transform.hasChanged;
            var check = Path.transform;
            while (check.parent != null && !hasChanged)
            {
                check = check.parent;
                hasChanged = check.hasChanged;
            }

            check.hasChanged = false;

            if (UpdateMode == UpdateType.PathOrTransformChanges || Path != _previousPath || hasChanged)
                ReconstructPath();
        }

        public override void OnActivate(HedgehogController controller)
        {
            if (controller == null) return;

            var index = Controllers.IndexOf(controller);
            if (index >= 0) return;

            var moveManager = controller.GetComponent<MoveManager>();
            if(moveManager != null) moveManager.Perform<Roll>(true);

            controller.Interrupt();

            Controllers.Add(controller);
            ControllerProgress.Add(0.0f);
            Velocities.Add(default(Vector2));
        }

        public override void OnActivateStay(HedgehogController controller)
        {
            if (controller == null) return;

            var index = Controllers.IndexOf(controller);
            if (index < 0) return;

            ControllerProgress[index] += TravelSpeed/_cachedLength*Time.fixedDeltaTime;

            var walk = SrPhysics2DUtility.Walk(_cachedPath, ControllerProgress[index], false) +
                       (Vector2) (controller.transform.position - controller.Sensors.Center.position);

            Velocities[index] = (walk - (Vector2)controller.transform.position)/Time.fixedDeltaTime;
            controller.transform.position = new Vector3(walk.x, walk.y, controller.transform.position.z);

            if (ControllerProgress[index] > 1.0f)
                EffectTrigger.Deactivate(controller);
        }

        public override void OnDeactivate(HedgehogController controller)
        {
            if (controller == null) return;

            var index = Controllers.IndexOf(controller);
            if (index < 0) return;

            controller.Velocity = Velocities[index].normalized*ExitSpeed;
            controller.Resume();

            Controllers.RemoveAt(index);
            ControllerProgress.RemoveAt(index);
            Velocities.RemoveAt(index);
        }

        /// <summary>
        /// Updates the platform's path with data from the collider stored in Path.
        /// </summary>
        public void ReconstructPath()
        {
            _previousPath = Path;
            Path.enabled = false;
            _cachedPath = SrPhysics2DUtility.GetPoints(Path);
            _cachedLength = SrPhysics2DUtility.GetPathLength(_cachedPath);
        }
    }
}
