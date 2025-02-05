﻿using ROSUnityCore;
using ROSUnityCore.ROSBridgeLib.sensor_msgs;
using ROSUnityCore.ROSBridgeLib.std_msgs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RobotAtVirtualHome
{
    public class LaserScanner : MonoBehaviour
    {

        [Header("General")]
        [Tooltip("The log level to use")]
        public LogLevel LogLevel = LogLevel.Normal;      

        [Range(0, 360)]
        public double angleMin = 0;
        [Range(0, 360)]
        public double angleMax = 360f;
        [Range(0, 360)]
        public double angleIncrement = 0.5f;

        [Range(0.01f, 100)]
        public double rangeMin = 0.12f;
        [Range(0.01f, 100)]
        public double rangeMax = 12.0f;

        [Header("ROS")]
        public bool sendScanToROS = true;
        [Range(0.01f, 10)]
        public float ROSFrecuency = 1f;     

        public double[] ranges { get; private set; }

        public Action<double[]> OnScanTaken;

        public LayerMask layerMask;

        #region Unity Functions
        private void Start()
        {
            int samples = (int)((angleMax - angleMin) / angleIncrement);
            ranges = new double[samples];
        }
        #endregion

        #region Public Functions
        public void Connected(ROS ros)
        {
            if (sendScanToROS)
            {
                ros.RegisterPubPackage("LaserScan_pub");
                StartCoroutine(SendLaser(ros));
            }
        }

        public double[] Scan()
        {
            Ray ray;
            for (double i = angleMin; i < angleMax; i += angleIncrement)
            {
                ray = new Ray(transform.position, Quaternion.Euler(0, 90 + (float)-i, 0) * transform.forward);
                if (Physics.Raycast(ray, out RaycastHit raycastHit, (float)rangeMax, layerMask))
                {

                    if (raycastHit.distance >= rangeMin && raycastHit.distance <= rangeMax)
                    {
                        ranges[(int)(i / angleIncrement)] = raycastHit.distance;
                    }
                }
            }
            OnScanTaken?.Invoke(ranges);
            return ranges;
        }

        public string GetTransformString()
        {
            return ((double)transform.position.x).ToString("F15", CultureInfo.InvariantCulture) + "," +
                    ((double)transform.position.y).ToString("F15", CultureInfo.InvariantCulture) + "," +
                    ((double)transform.position.z).ToString("F15", CultureInfo.InvariantCulture) + "," +
                    ((double)transform.rotation.eulerAngles.x).ToString("F15", CultureInfo.InvariantCulture) + "," +
                    ((double)transform.rotation.eulerAngles.y).ToString("F15", CultureInfo.InvariantCulture) + "," +
                    ((double)transform.rotation.eulerAngles.z).ToString("F15", CultureInfo.InvariantCulture);
        }
        #endregion

        #region Private Functions
        private IEnumerator SendLaser(ROS ros)
        {
            Log("Sending laser to ros.",LogLevel.Developer);
            while (ros.IsConnected())
            {
                yield return new WaitForEndOfFrame();
                HeaderMsg _head = new HeaderMsg(0, new TimeMsg(DateTime.Now.Second, 0), transform.name);
                LaserScanMsg scan = new LaserScanMsg(_head, angleMin * Mathf.Deg2Rad, angleMax * Mathf.Deg2Rad, angleIncrement * Mathf.Deg2Rad, 0, 0, rangeMin, rangeMax, Scan(), new double[0]);
                ros.Publish(LaserScan_pub.GetMessageTopic(), scan);
                yield return new WaitForSeconds(ROSFrecuency);
            }
        }

        private void Log(string _msg, LogLevel lvl, bool Warning = false)
        {
            if (LogLevel <= lvl && LogLevel != LogLevel.Nothing)
            {
                if (Warning)
                {
                    Debug.LogWarning("[Laser Scanner]: " + _msg);
                }
                else
                {
                    Debug.Log("[Laser Scanner]: " + _msg);
                }
            }
        }
        #endregion
    }
}