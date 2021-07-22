﻿using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace RobotAtVirtualHome {

    [RequireComponent(typeof(NavMeshAgent))]

    public class AIGrid : VirtualAgent {

        public Vector2 minRange;
        public Vector2 maxRange;
        public float cellSize = 0.5f;
        public bool exactRoute = false;
        public int photosPerPoint = 10;

        public bool captureRGB;
        public bool captureDepth;
        public bool captureSemanticMask;
        public bool captureScan;

        public string filePath { get; private set; }
        private StreamWriter logImgWriter;
        private StreamWriter logScanWriter;

        public string room { get; private set; }        

        private List<Vector3> grid;
        private int index = 0;

        #region Unity Functions

        void Start() {

            if (minRange[0] >= maxRange[0] || minRange[1] >= maxRange[1]) {
                LogWarning("Incorrect ranges");
            }

            if (captureRGB || captureDepth || captureSemanticMask || captureScan) {
                filePath = FindObjectOfType<GeneralSystem>().path;
                string tempPath = Path.Combine(filePath, "Grid");
                int i = 0;
                while (Directory.Exists(tempPath)) {
                    i++;
                    tempPath = Path.Combine(filePath, "Grid" + i);
                }

                filePath = tempPath;
                if (!Directory.Exists(filePath)) {
                    Directory.CreateDirectory(filePath);
                }

                if (captureRGB || captureDepth || captureSemanticMask) {

                    logImgWriter = new StreamWriter(filePath + "/InfoGrid.csv", true);
                    logImgWriter.WriteLine("photoID;robotPosition;robotRotation;cameraPosition;cameraRotation;room");
                }

                if (captureScan) {
                    logScanWriter = new StreamWriter(filePath + "/LogScan.csv", true);
                    logScanWriter.WriteLine("scanID;robotPosition;robotRotation;data");
                }

                Log("The saving path is:" + filePath);
            }
            state = StatusMode.Loading;
            grid = new List<Vector3>();
            StartCoroutine(CalculateGrid());           
        }

        private void Update() {
            switch (state) {
                case StatusMode.Walking:

                    RaycastHit hit;
                    if (Physics.Raycast(transform.position, transform.TransformDirection(Vector3.down), out hit)) {
                        room = hit.transform.name;
                    }

                    if (agent.remainingDistance <= agent.stoppingDistance &&
                        agent.velocity.sqrMagnitude == 0f) {
                        state = StatusMode.Turning;
                        StartCoroutine(Capture());
                        Log("Change state to Capture");
                    }
                    break;
            }
        }

        private void OnDestroy() {
            if (logImgWriter != null) {
                logImgWriter.Close();
            }
            if (logScanWriter != null) {
                logScanWriter.Close();
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            if (Application.isPlaying && this.enabled && verbose > 0) {
                Gizmos.color = Color.green;
                foreach (Vector3 point in grid) {
                    Gizmos.DrawSphere(point, 0.1f);
                }
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(agent.destination, 0.2f);
            }
        }
#endif

        #endregion

        #region Public Functions
        #endregion

        #region Private Functions
        private IEnumerator CalculateGrid() {
            NavMeshPath path = new NavMeshPath();
            for (float i = minRange[0]; i <= maxRange[0]; i += cellSize) {
                for (float j = minRange[1]; j <= maxRange[1]; j += cellSize) {
                    Vector3 point = new Vector3(i, transform.position.y, j);
                    agent.CalculatePath(point, path);
                    if (exactRoute) {
                        if (path.status == NavMeshPathStatus.PathComplete && Vector3.Distance(path.corners[path.corners.Length - 1], point) < 0.04f) {
                            grid.Add(point);
                        }
                    } else {
                        if (path.status == NavMeshPathStatus.PathComplete) {
                            grid.Add(point);
                        }
                    }
                }
            }
            Debug.Log("Nodos: " + grid.Count);
            yield return new WaitForEndOfFrame();
            agent.SetDestination(grid[index]);
            agent.isStopped = false;
            yield return new WaitForEndOfFrame();
            state = StatusMode.Walking;
            Log("Start");

            yield return null;
        }

        private IEnumerator Capture() {
            transform.rotation = Quaternion.identity;
            yield return new WaitForEndOfFrame();

            if (captureScan) {
                string data = "";
                foreach (float d in laserScan.ranges) {
                    data += d.ToString() + ";";
                }
                logScanWriter.WriteLine(index.ToString() + transform.position + ";" + transform.rotation.eulerAngles + ";" + data);
            }

            byte[] bytes;
            for (int i = 1; i <= photosPerPoint; i++) {
                yield return new WaitForSeconds(0.75f);
                if (captureRGB) {
                    logImgWriter.WriteLine(index.ToString() + "_" + i.ToString() + "_rgb.png;"
                        + transform.position.ToString("F6") + ";"
                        + transform.rotation.eulerAngles.ToString("F6") + ";"
                        + smartCamera.transform.localPosition.ToString("F6") + ";"
                        + smartCamera.transform.localRotation.eulerAngles.ToString("F6") + ";"
                        + room);
                        bytes = smartCamera.ImageRGB.EncodeToPNG();
                    File.WriteAllBytes(filePath + "/" + index.ToString() + "_" + i.ToString() + "_rgb.png", bytes);
                }
                if (captureDepth) {
                    logImgWriter.WriteLine(index.ToString() + "_" + i.ToString() + "_depth.png;"
                        + transform.position.ToString("F6") + ";"
                        + transform.rotation.eulerAngles.ToString("F6") + ";"
                        + smartCamera.transform.localPosition.ToString("F6") + ";"
                        + smartCamera.transform.localRotation.eulerAngles.ToString("F6") + ";"
                        + room);
                    bytes = smartCamera.ImageDepth.EncodeToPNG();
                    File.WriteAllBytes(filePath + "/" + index.ToString() + "_" + i.ToString() + "depth.png", bytes);
                }
                if (captureSemanticMask) {
                    logImgWriter.WriteLine(index.ToString() + "_" + i.ToString() + "_mask.png;"
                        + transform.position.ToString("F6") + ";"
                        + transform.rotation.eulerAngles.ToString("F6") + ";"
                        + smartCamera.transform.localPosition.ToString("F6") + ";"
                        + smartCamera.transform.localRotation.eulerAngles.ToString("F6") + ";"
                        + room);
                    bytes = smartCamera.ImageMask.EncodeToPNG();
                    File.WriteAllBytes(filePath + "/" + index.ToString() + "_" + i.ToString() + "_mask.png", bytes);
                }                

                bytes = null;
                transform.rotation = Quaternion.Euler(0, i * (360 / photosPerPoint), 0);
            }

            Log(index.ToString() + "/" + grid.Count + " - " + (index / (float)grid.Count) * 100 + "%");
            index++;
            if (index >= grid.Count) {
                state = StatusMode.Finished;
                Log("Finished");
                GetComponent<AudioSource>().Play();
            } else {
                agent.SetDestination(grid[index]);
                agent.isStopped = false;
                state = StatusMode.Walking;
                Log(grid[index].ToString());
            }

            yield return null;
        }

        #endregion
    }
}