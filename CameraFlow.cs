﻿using SonsSdk;
using SonsSdk.Attributes;
using TheForest.Utils;
using UnityEngine;
using RedLoader;
using System.Collections;
using TheForest;
using SUI;
using static SUI.SUI;
using System.Text.Json;
using System.Text.Json.Serialization;
using Endnight.Utilities;
using Sons.Gui;
using Endnight.Physics;
using System.ComponentModel;

namespace CameraFlow;

public class CameraFlow : SonsMod
{
    public CameraFlow()
    {
        //OnUpdateCallback = MyUpdateMethod;
        OnFixedUpdateCallback = MyUpdateMethod;
    }

    protected override void OnInitializeMod()
    {
        Config.Init();
    }

    protected override void OnSdkInitialized()
    {
        CameraFlowUi.Create();
        var panel = GetPanel("SidePanel");
        panel.Active(false);



        // Add in-game settings ui for your mod.
        SettingsRegistry.CreateSettings(this, null, typeof(Config));
        speed = Config.Speed.Value;
        resolution = Config.Resolution.Value;
        Config.MenuKey.Notify(KeyPressMethod);
        Config.PosKey.Notify(setPosition);
        Config.DrawKey.Notify(drawToggle);
        Config.StartCamKey.Notify(StartMoving);


        GameObject go = new GameObject("CameraMoverObject");
        go.AddComponent<CameraMover>();
    }

    protected override void OnGameStart()
    {
        
    }

    protected override void OnSonsSceneInitialized(ESonsScene sonsScene)
    {
        if (sonsScene == ESonsScene.Title || sonsScene == ESonsScene.Loading)
        {
            if (isMoving)
            {
                ToggleFreeCamera(false);
                toggleUI(true);
                ToggleGodMode(false);
            }

            isMoving = false;
            clearCalculatedPaths();
            currentlyDrawing = false;

            
        }
    }

    private static int currentTargetIndex = 0;
    private static bool isMoving = false;
    public static int speed = 1;  // Speed of the movement
    public static List<Vector3> positions = new List<Vector3>();
    public static List<Quaternion> rotations = new List<Quaternion>();
    public static List<Vector3> calculatedPath = new List<Vector3>();
    public static List<Quaternion> calculatedRotations = new List<Quaternion>();
    public static List<Vector3> finalCalculatedPath = new List<Vector3>();
    public static List<Quaternion> finalCalculatedRotations = new List<Quaternion>();
    public static List<float> segmentLengths = new List<float>();
    public static int positionsAlreadyCalculated = 0;
    public static int resolution = 100;
    private static List<GameObject> cubes = new List<GameObject>();
    private static List<SonsSdk.DebugTools.LineDrawer> lines = new List<SonsSdk.DebugTools.LineDrawer>();
    private static List<SonsSdk.DebugTools.LineDrawer> lineDrawers = new List<SonsSdk.DebugTools.LineDrawer>();
    private static bool panelActive = false;
    public static bool currentlyDrawing = false;
    public static int selectedPoint = 0;
    private static Vector3 startPos = Vector3.zero;
    public static float tSegment = 0f;
    public static float totalLength = 0f;
    public static float easingMultiplier = 2f;
    public static string fileLocation = $"Mods/CameraFlow/";

    //B-Splines
    public static void CalculatePath(bool force)
    {
        if (force)
        {
            positionsAlreadyCalculated = 0;
        }

        if(positions.Count == positionsAlreadyCalculated)
        {
            return;
        }
        List<Vector3> positionsTemporary = new List<Vector3>();
        List<Quaternion> rotationsTemporary = new List<Quaternion>();
        List<bool?> easingsTemporary = new List<bool?>();
        List<float> easingMultipliersTemporary = new List<float>();

        if (resolution != Config.Resolution.Value)
        {
            resolution = Config.Resolution.Value;
        }

        if (speed != Config.Speed.Value)
        {
            speed = Config.Speed.Value;
        }

        //destroy path before starting
        if (currentlyDrawing) { drawPath(false); }

        if (positions.Count < 4)  // Need at least 4 points for B-Splines
        {
            return;
        }

        clearCalculatedPaths();

        // Calculate the direction of the first segment
        Vector3 firstSegmentDirection = (positions[1] - positions[0]).normalized;

        // Calculate the position of the imaginary point
        Vector3 imaginaryPoint = positions[0] - firstSegmentDirection * 10;  // Adjust the multiplier as needed

        // Calculate the direction of the last segment
        Vector3 lastSegmentDirection = (positions[positions.Count - 1] - positions[positions.Count - 2]).normalized;

        // Calculate the position of the imaginary point
        Vector3 lastImaginaryPoint = positions[positions.Count - 1] + lastSegmentDirection * 10;  // Adjust the multiplier as needed

        easingsTemporary.Add(false);

        easingMultipliersTemporary.Add(1.2f);

        rotationsTemporary.Add(rotations[0]);

        //add imaginaryPoint to positionsTemporary
        positionsTemporary.Add(imaginaryPoint);
        
        //add all other positions to positionsTemporary
        for (int i = 0; i < positions.Count; i++)
        {
            positionsTemporary.Add(positions[i]);
            rotationsTemporary.Add(rotations[i]);
        }

        //add lastImaginaryPoint to positionsTemporary
        positionsTemporary.Add(lastImaginaryPoint);

        rotationsTemporary.Add(rotations[rotations.Count - 1]);

        easingsTemporary.Add(false);

        easingMultipliersTemporary.Add(1.2f);

        // Calculate the lengths of all segments and the total length of the path
        for (int i = 0; i < positionsTemporary.Count - 3; i++)
        {
            float segmentLength = CalculateBSplineLength(positionsTemporary[i], positionsTemporary[i + 1], positionsTemporary[i + 2], positionsTemporary[i + 3], resolution);
            segmentLengths.Add(segmentLength);
            totalLength += segmentLength;
        }

        // Decide on the spacing you want between points
        float spacing = 1.0f / resolution; // This will give you 'resolution' number of points per unit distance

        // Calculate the positions and rotations of the points
        float accumulatedLength = 0;
        int segmentIndex = 0;
        while (segmentIndex < positionsTemporary.Count - 3)
        {
            tSegment = Mathf.Clamp01(accumulatedLength / segmentLengths[segmentIndex]);

            // Calculate the position at this point on the curve
            Vector3 pointOnCurve = CalculateBSplinePoint(tSegment, positionsTemporary[segmentIndex], positionsTemporary[segmentIndex + 1], positionsTemporary[segmentIndex + 2], positionsTemporary[segmentIndex + 3]);
            calculatedPath.Add(pointOnCurve);

            Quaternion rotOnCurve = CalculateBSplineRotation(tSegment,
                                                 rotationsTemporary[segmentIndex],
                                                 rotationsTemporary[segmentIndex + 1],
                                                 rotationsTemporary[segmentIndex + 2],
                                                 rotationsTemporary[segmentIndex + 3]);
            calculatedRotations.Add(rotOnCurve);

            accumulatedLength += spacing;


            // If we've moved beyond the current segment
            while (segmentIndex < positionsTemporary.Count - 3 && accumulatedLength > segmentLengths[segmentIndex])
            {
                // Subtract the length of the current segment and move on to the next one
                accumulatedLength -= segmentLengths[segmentIndex];
                segmentIndex++;
            }
        }

        // Now we have a lookup table with a fine resolution
        // Let's step through it to find points that are as close to equal distance as possible
        float targetDistance = speed / 100f;
        float currentDistance = 0;
        Vector3 previousPoint = calculatedPath[0];

        finalCalculatedPath.Add(previousPoint);
        finalCalculatedRotations.Add(calculatedRotations[0]);

        for (int i = 1; i < calculatedPath.Count; i++)
        {
            // If this is the last point and it's the same as the first point, skip it
            if (i == calculatedPath.Count - 1 && calculatedPath[i] == calculatedPath[0])
            {
                continue;
            }

            currentDistance += Vector3.Distance(previousPoint, calculatedPath[i]);

            if (currentDistance >= targetDistance)
            {
                finalCalculatedPath.Add(calculatedPath[i]);
                finalCalculatedRotations.Add(calculatedRotations[i]);
                previousPoint = calculatedPath[i];
                currentDistance = 0;
            } else if (currentDistance < targetDistance)
            {
                previousPoint = calculatedPath[i];
            }
        }

        startPos = finalCalculatedPath[0];
        positionsAlreadyCalculated = positions.Count;
        drawRefresh();
    }

    public static Vector3 CalculateBSplinePoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float it = 1.0f - t;

        Vector3 b0 = p0 * (-t * t * t + 3 * t * t - 3 * t + 1);
        Vector3 b1 = p1 * (3 * t * t * t - 6 * t * t + 4);
        Vector3 b2 = p2 * (-3 * t * t * t + 3 * t * t + 3 * t + 1);
        Vector3 b3 = p3 * t * t * t;

        return (b0 + b1 + b2 + b3) / 6.0f;
    }

    public static Quaternion CalculateBSplineRotation(float t, Quaternion q0, Quaternion q1, Quaternion q2, Quaternion q3)
    {
        float u = t;

        // B-spline blending functions (same as before)
        float a0 = (1 - u) * (1 - u) * (1 - u) / 6f;
        float a1 = (3 * u * u * u - 6 * u * u + 4) / 6f;
        float a2 = (-3 * u * u * u + 3 * u * u + 3 * u + 1) / 6f;
        float a3 = u * u * u / 6f;

        // Combine weighted quaternions using Slerp (multiple times for accuracy)
        Quaternion result = Quaternion.Slerp(q0, q1, a1 / (a0 + a1));
        result = Quaternion.Slerp(result, q2, a2 / (a0 + a1 + a2));
        result = Quaternion.Slerp(result, q3, a3 / (a0 + a1 + a2 + a3));

        return result.normalized;
    }

    public static float CalculateBSplineLength(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, int resolution)
    {
        float length = 0.0f;
        Vector3 previousPoint = CalculateBSplinePoint(0, p0, p1, p2, p3);

        for (int i = 1; i <= resolution; i++)
        {
            float t = (float)i / resolution;
            Vector3 currentPoint = CalculateBSplinePoint(t, p0, p1, p2, p3);
            length += Vector3.Distance(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }

        return length;
    }

    public void StartMoving()
    {
        if(speed != Config.Speed.Value)
        {
            speed = Config.Speed.Value;
        }

        if(isMoving)
        {
            StopMoving();
            return;
        }

        if (positions.Count < 4)
        {
            SonsTools.ShowMessage("Not enough points to calculate path");
            return;
        }

        else {
            CalculatePath(false);
            SetCameraMovementSittingFix(false);
            LocalPlayer.RaceSystem.SwitchToThirdPerson();
            ToggleFreeCamera(true);
            toggleUI(false);
            ToggleGodMode(true);
            SetCameraMovement(false);
            currentTargetIndex = 0;
            // Set the camera's position to the first position in the list
            GameObject freeCam = GameObject.Find("MainCameraFP");
            if (positions.Count > 0)
            {
                freeCam.transform.position = startPos;
                freeCam.transform.rotation = rotations[0];
            }
            isMoving = true;
            //StartMoveCamera();
        }
    }

    
    private static void clearCalculatedPaths()
    { 
        calculatedPath.Clear();
        calculatedRotations.Clear();
        finalCalculatedPath.Clear();
        finalCalculatedRotations.Clear();
        segmentLengths.Clear();
        totalLength = 0;
        positionsAlreadyCalculated = 0;
    }

    public void StopMoving()
    {
        //StopMoveCamera();
        LocalPlayer.RaceSystem.SwitchToFirstPerson();
        SetCameraMovementSittingFix(true);
        SetCameraMovement(true);
        isMoving = false;
        //DebugConsole.Instance.SendCommand("freecamera off");
        ToggleFreeCamera(false);
        toggleUI(true);
        if (!Config.Godmode.Value)
        {
            ToggleGodMode(false);
        }
    }



    private void MyUpdateMethod()
    {
        CameraFlowUi.Update();
        moveCamera();
    }

    private void moveCamera()
    {
        if(!isMoving)
        {
            return;
        }

        if (isMoving && currentTargetIndex < finalCalculatedPath.Count - 1)
        {
            GameObject freeCam = GameObject.Find("MainCameraFP");
            float moveSpeed = 0.1f * speed; // Adjust this value to control overall speed

            // Calculate the maximum distance the camera can move this frame
            float maxDistanceDelta = Time.deltaTime * moveSpeed;

            // Calculate the direction to the target
            Vector3 directionToTarget = (finalCalculatedPath[currentTargetIndex + 1] - freeCam.transform.position).normalized;

            // Move the camera's position along the direction
            Vector3 oldPosition = freeCam.transform.position;
            freeCam.transform.position += directionToTarget * maxDistanceDelta;
            Vector3 directionMoved = (freeCam.transform.position - oldPosition).normalized;

            // Smoothly rotate the camera to the stored rotation
            float distanceMoved = (freeCam.transform.position - finalCalculatedPath[currentTargetIndex]).magnitude;
            float totalDistance = (finalCalculatedPath[currentTargetIndex + 1] - finalCalculatedPath[currentTargetIndex]).magnitude;
            float t = (totalDistance > 0) ? distanceMoved / totalDistance : 0;

            // Smoothly rotate the camera to the stored rotation
            freeCam.transform.rotation = Quaternion.Slerp(finalCalculatedRotations[currentTargetIndex], finalCalculatedRotations[currentTargetIndex + 1], t);

            // Check if the camera has moved past the target
            if (Vector3.Dot(directionToTarget, directionMoved) < 0)
            {
                currentTargetIndex++;
            }
            else if (Vector3.Distance(freeCam.transform.position, finalCalculatedPath[currentTargetIndex + 1]) < maxDistanceDelta)
            {
                currentTargetIndex++;
            }

            if (currentTargetIndex >= finalCalculatedPath.Count - 1)
            {
                StopMoving();
                SonsTools.ShowMessage("Camera flow ended");
            }
        }
        else if (isMoving && currentTargetIndex >= finalCalculatedPath.Count - 2)
        {
            StopMoving();
            SonsTools.ShowMessage("Camera flow ended");
        }
    }

    public static void drawPath(bool drawOrClear)
    {
        if (calculatedPath.Count > 0)
        {
            if (drawOrClear)
            {
                var pathPointsCalculated = finalCalculatedPath;

                //DrawLine(pathPointsCalculated, 0.1f, 0.1f, Color.green);
                //DrawLine(positions, 0.05f, 0.05f, Color.red);

                for (int i = 0; i < positions.Count -1; i++)
                {
                    var List = new List<Vector3> { positions[i], positions[i + 1] };
                    DrawLine(List, 0.02f, 0.02f, Color.red);
                }

                int segmentCount = positions.Count - 1;
                int pointsPerSegment = pathPointsCalculated.Count / segmentCount;

                for (int i = 0; i < segmentCount; i++)
                {
                    int start = i * pointsPerSegment;
                    int end = (i == segmentCount - 1) ? pathPointsCalculated.Count : start + pointsPerSegment;

                    var segmentPoints = pathPointsCalculated.GetRange(start, end - start);

                    // If it's not the last segment, add the first point of the next segment to the current segment
                    if (i != segmentCount - 1)
                    {
                        segmentPoints.Add(pathPointsCalculated[end]);
                    }

                    DrawLine(segmentPoints, 0.05f, 0.05f, Color.green);
                }

                for (int i = 0; i < positions.Count; i++)
                {
                    var position = positions[i];
                    var rotation = rotations[i];
                    if (i == selectedPoint)
                    {
                        var cube = SonsSdk.DebugTools.CreateCuboid(position, new Vector3(0.3f, 0.3f, 0.3f), Color.yellow, false);
                        cubes.Add(cube.gameObject);
                    }
                    else
                    {
                        var cube = SonsSdk.DebugTools.CreateCuboid(position, new Vector3(0.2f, 0.2f, 0.2f), Color.gray, false);
                        cubes.Add(cube.gameObject);
                    }
                   

                    // Draw lines representing the x, y, and z axes
                    var xLine = new SonsSdk.DebugTools.LineDrawer();
                    xLine.SetLine(position, position + Vector3.right);
                    xLine.LineRenderer.material.color = Color.red;
                    xLine.LineRenderer.startWidth = 0.08f;
                    xLine.LineRenderer.endWidth = 0.03f;
                    lines.Add(xLine);

                    var yLine = new SonsSdk.DebugTools.LineDrawer();
                    yLine.SetLine(position, position + Vector3.up);
                    yLine.LineRenderer.material.color = Color.green;
                    yLine.LineRenderer.startWidth = 0.08f;
                    yLine.LineRenderer.endWidth = 0.03f;
                    lines.Add(yLine);

                    var zLine = new SonsSdk.DebugTools.LineDrawer();
                    zLine.SetLine(position, position + Vector3.forward);
                    zLine.LineRenderer.material.color = Color.blue;
                    zLine.LineRenderer.startWidth = 0.08f;
                    zLine.LineRenderer.endWidth = 0.03f;
                    lines.Add(zLine);

                    // Draw a line in the direction the rotation is facing
                    var rotationLine = new SonsSdk.DebugTools.LineDrawer();
                    rotationLine.SetLine(position, position + rotation * Vector3.forward);  // Multiply the rotation by Vector3.forward to get the forward vector of the rotation
                    rotationLine.LineRenderer.material.color = Color.white;
                    rotationLine.LineRenderer.startWidth = 0.1f;
                    rotationLine.LineRenderer.endWidth = 0.1f;
                    lines.Add(rotationLine);
                }
                
            }
            else
            {
                if (lineDrawers.Count != 0)
                {
                    removePath();
                }
            }
        }
    }

    private static void DrawLine(List<Vector3> points, float startWidth, float endWidth, Color color)
    {
        // Draw the line from front to back
        var lineDrawer = new SonsSdk.DebugTools.LineDrawer();
        var lineRenderer = lineDrawer.LineRenderer;
        lineRenderer.startWidth = startWidth;
        lineRenderer.endWidth = endWidth;
        lineRenderer.material.color = color;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.numCapVertices = 5;
        lineRenderer.positionCount = points.Count;
        lineRenderer.SetPositions(points.ToArray());
        lineRenderer.enabled = true;
        lineDrawers.Add(lineDrawer);

        // Draw the line from back to front
        var pointsBackwards = new List<Vector3>(points);
        pointsBackwards.Reverse();

        lineDrawer = new SonsSdk.DebugTools.LineDrawer();
        lineRenderer = lineDrawer.LineRenderer;
        lineRenderer.startWidth = startWidth;
        lineRenderer.endWidth = endWidth;
        lineRenderer.material.color = color;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.numCapVertices = 5;
        lineRenderer.positionCount = points.Count;
        lineRenderer.SetPositions(pointsBackwards.ToArray());
        lineRenderer.enabled = true;
        lineDrawers.Add(lineDrawer);
    }

    private static void removePath()
    {
        if (lineDrawers.Count != 0)
        {
            foreach (var lineDrawer in lineDrawers)
            {
                lineDrawer.Destroy();
            }
            lineDrawers.Clear();

            foreach (var cube in cubes)
            {
                GameObject.Destroy(cube);
            }
            cubes.Clear();

            foreach (var line in lines)
            {
                line.Destroy();
            }
            lines.Clear();
        }
    }

    public static void removePathAndPositions()
    {
        removePath();
        positions.Clear();
        rotations.Clear();
    }

    private void drawToggle()
    {
        if (currentlyDrawing)
        {
            drawPath(false);
            currentlyDrawing = false;
            SonsTools.ShowMessage("Preview Disabled");
        }
        else
        {
            drawPath(true);
            currentlyDrawing = true;
            SonsTools.ShowMessage("Preview Enabled");
        }
    }

    public static void drawRefresh()
    {
        if (currentlyDrawing) {
            drawPath(true);
        }
    }

    public static void setPosition()
    {
        GameObject freeCam = GameObject.Find("MainCameraFP");
        Quaternion playerRot = Quaternion.identity;
        //add the current position of the player to positions list
        Vector3 playerPos = Vector3.zero;
        if (freeCam) {
            playerPos = freeCam.transform.position;
            playerRot = freeCam.transform.rotation;
        } else
        {
            playerPos = LocalPlayer.Transform.position;
            //increase height to be roughly head height
            playerPos.y += 1.5f;

            playerRot = LocalPlayer.Transform.rotation;
        }
        
        

        // Get the Euler angles (rotation around x, y, and z axes) of the player and camera
        Vector3 playerEulerAngles = playerRot.eulerAngles;
        Vector3 cameraEulerAngles = freeCam.transform.rotation.eulerAngles;

        // Create a new rotation that combines the player's and camera's rotations
        Quaternion combinedRot = Quaternion.Euler(cameraEulerAngles.x, playerEulerAngles.y, playerEulerAngles.z);

        
        positions.Add(playerPos);
        rotations.Add(combinedRot);
        //if there are not enough positions to calculate the path show a message how many are left
        if (positions.Count < 4)
        {
            SonsTools.ShowMessage("Position added to list, " + (4 - positions.Count) + " more needed to calculate path");
        }
        else
        {
            SonsTools.ShowMessage("Position added to list");
            CalculatePath(true);
        }
    }

    public bool IsInFreeCam()
    {
        // Get the MainCameraFP and FreeCameraController objects
        GameObject mainCameraFP = GameObject.Find("MainCameraFP");
        GameObject freeCameraController = GameObject.Find("FreeCameraController");

        // Check if MainCameraFP is a child of FreeCameraController
        if (mainCameraFP.transform.parent == freeCameraController.transform)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    public void SetCameraMovement(bool enable)
    {
        // Get the FreeCameraController object
        GameObject freeCameraController = GameObject.Find("FreeCameraController");

        // Get the Sons.Gui.FreeCameraController component
        var freeCameraControllerComponent = freeCameraController.GetComponent<Sons.Gui.FreeCameraController>();

        // Enable or disable the component
        freeCameraControllerComponent.enabled = enable;
    }

    public void SetCameraMovementSittingFix(bool enable)
    {
        // Get the FreeCameraController object
        GameObject freeCameraController = GameObject.Find("MainCameraFP");

        // Get the Sons.Gui.FreeCameraController component
        var freeCameraControllerComponent = freeCameraController.GetComponent<SimpleMouseRotator>();

        // Enable or disable the component
        freeCameraControllerComponent.enabled = enable;
    }
    
    public class SerializableVector3
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public SerializableVector3(Vector3 vector)
        {
            X = vector.x;
            Y = vector.y;
            Z = vector.z;
        }
    }

    public class SerializableQuaternion
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float W { get; set; }

        public SerializableQuaternion(Quaternion quaternion)
        {
            X = quaternion.x;
            Y = quaternion.y;
            Z = quaternion.z;
            W = quaternion.w;
        }
    }

    public static void SaveCameraFlowData(string fileName)
    {
        try
        {
            if (positions.Count == 0)
            {
                SonsTools.ShowMessage("No path found!");
                return;
            }

            // Convert positions and rotations to serializable types
            var serializablePositions = positions.Select(p => new SerializableVector3(p)).ToList();
            var serializableRotations = rotations.Select(r => new SerializableQuaternion(r)).ToList();

            // Serialize the data to JSON
            var data = new { 
                fileName, 
                positions = serializablePositions, 
                rotations = serializableRotations,
            };
            var options = new JsonSerializerOptions { ReferenceHandler = ReferenceHandler.Preserve };
            string json = JsonSerializer.Serialize(data);

            // Write the JSON to a file Mods\CameraFlow
            string path = fileLocation + fileName + ".json";
            File.WriteAllText(path, json);
            SonsTools.ShowMessage(fileName + " Saved successfully!");
            SonsTools.ShowMessage("File will appear in the list after restarting");
        }
        catch (Exception ex)
        {
            // Log the exception
            RLog.Error("An error occurred while saving camera flow data: " + ex.ToString());
        }
    }
    
    public static void toggleUI(bool enable)
    {
        Sons.Settings.GameplaySettings.SetAllGuiChanged(enable);
    }

    private void ToggleFreeCamera(bool enable)
    {
        if (enable)
        {
            DebugConsole.Instance._freecamera("on");
        }
        else
        {
            DebugConsole.Instance._freecamera("off");
        }
    }

    private void ToggleGodMode(bool enable)
    {
        if (enable)
        {
            DebugConsole.Instance._godmode("on");
        }
        else
        {
            DebugConsole.Instance._godmode("off");
        }
    }

    public class SerializableVector3Load
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    public class SerializableQuaternionLoad
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float W { get; set; }
    }

    class CameraFlowData
    {
        public string FileName { get; set; }
        public List<SerializableVector3Load> Positions { get; set; }
        public List<SerializableQuaternionLoad> Rotations { get; set; }
        public List<bool?> Easings { get; set; }
        public List<float> EasingMultipliers { get; set; }
    }


    public static void LoadCameraFlowData(string fileName)
    {
        try
        {
            string path = fileName;
            string json = File.ReadAllText(path);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };

            CameraFlowData data = JsonSerializer.Deserialize<CameraFlowData>(json, options);

            positions.Clear();
            rotations.Clear();

            foreach (var item in data.Positions)
            {
                positions.Add(new Vector3(item.X, item.Y, item.Z));
            }
            foreach (var item in data.Rotations)
            {
                rotations.Add(new Quaternion(item.X, item.Y, item.Z, item.W));
            }

            CalculatePath(true);
            SonsTools.ShowMessage(path + " Loaded successfully!");
        }
        catch (Exception ex)
        {
            RLog.Error("File not found or no longer exists! " + ex.ToString());
            SonsTools.ShowMessage("File not found or no longer exists!");
        }
    }

    public static void DeleteCameraFlowData(string fileName)
    {
        try
        {
            // Check if the file exists before trying to delete it
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
                SonsTools.ShowMessage(fileName + " deleted successfully! Will dissapear after a restart");
            }
            else
            {
                SonsTools.ShowMessage("File not found: " + fileName);
            }
        }
        catch (Exception ex)
        {
            RLog.Error("An error occurred while deleting camera flow data: " + ex.ToString());
        }
    }



    //Commands
    [DebugCommand("DebugCameraFlow")]
    private void DebugCameraFlow()
    {
        RLog.Error("Debugging Camera Flow");
        RLog.Msg("Speed: " + speed + "  Speed calculated:  " + Time.deltaTime * (speed * 0.1f));
        RLog.Msg("Positions: " + positions.Count);
        RLog.Msg("Rotations: " + rotations.Count);
        RLog.Msg("Calculated Path: " + calculatedPath.Count);
        RLog.Msg("Calculated Rotations: " + calculatedRotations.Count);
        if(calculatedPath.Count != calculatedRotations.Count)
        {
            RLog.Error("DESYNC!!! Calculated Path and Calculated Rotations are not equal");
        }
        RLog.Msg("Segment Lengths: " + segmentLengths.Count);
        RLog.Msg("Total Length: " + totalLength);
        var ppu = calculatedPath.Count / totalLength;
        RLog.Msg("Positiions per unit: " + ppu);
        (float min, float max, float average) = CalculateDistances();
        RLog.Msg($"Min distance: {min}, Max distance: {max}, Average distance: {average}");


        (float minF, float maxF, float averageF) = CalculateDistancesFinal();
        RLog.Msg($"Min distance final: {minF}, Max distance final: {maxF}, Average distance final: {averageF}");
        RLog.Msg("Final Points Amount: " + finalCalculatedPath.Count);

        RLog.Error("End Debugging Camera Flow");
    }

    public (float min, float max, float average) CalculateDistances()
    {
        if (calculatedPath.Count < 2)
        {
            return (0, 0, 0);
        }

        float minDistance = float.MaxValue;
        float maxDistance = float.MinValue;
        float totalDistance = 0;

        for (int i = 0; i < calculatedPath.Count - 1; i++)
        {
            float distance = Vector3.Distance(calculatedPath[i], calculatedPath[i + 1]);
            minDistance = Mathf.Min(minDistance, distance);
            maxDistance = Mathf.Max(maxDistance, distance);
            totalDistance += distance;
        }

        float averageDistance = totalDistance / (calculatedPath.Count - 1);

        return (minDistance, maxDistance, averageDistance);
    }

    public (float min, float max, float average) CalculateDistancesFinal()
    {
        if (finalCalculatedPath.Count < 2)
        {
            return (0, 0, 0);
        }

        float minDistance = float.MaxValue;
        float maxDistance = float.MinValue;
        float totalDistance = 0;

        for (int i = 0; i < finalCalculatedPath.Count - 1; i++)
        {
            float distance = Vector3.Distance(finalCalculatedPath[i], finalCalculatedPath[i + 1]);
            minDistance = Mathf.Min(minDistance, distance);
            maxDistance = Mathf.Max(maxDistance, distance);
            totalDistance += distance;
        }

        float averageDistance = totalDistance / (finalCalculatedPath.Count - 1);

        return (minDistance, maxDistance, averageDistance);
    }
    //SUI Bullshittery

    public void KeyPressMethod()
    {
        var panel = GetPanel("SidePanel");
        panelActive = !panelActive;
        if (panelActive)
        {
            SonsTools.ShowMessage("Opening Camera Flow Menu");
            panel.Opacity(Config.Opacity.Value);
            panel.Active(true);
        }
        else
        {
            SonsTools.ShowMessage("Closing Camera Flow Menu");
            panel.Active(false);
        }
    }
}

public class CameraMover : MonoBehaviour
{
    public static CameraMover Instance; // Singleton instance
    public static CameraFlow CameraFlow;

    private void Awake()
    {
        Instance = this; // Assign the singleton instance in Awake
    }
}