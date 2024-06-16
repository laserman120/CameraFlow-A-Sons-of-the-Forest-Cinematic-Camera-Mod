using SonsSdk;
using SonsSdk.Attributes;
using TheForest.Utils;
using UnityEngine;
using RedLoader;
using TheForest;
using SUI;
using static SUI.SUI;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sons.Animation;
using Newtonsoft.Json.Linq;
using System.ComponentModel.Design;

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
        Config.MenuKey.Notify(MenuToggle);
        Config.PosKey.Notify(setPosition);
        Config.DrawKey.Notify(drawToggle);
        Config.StartCamKey.Notify(StartMoving);

        if(Config.ForceUi.Value)
        {
            toggleUI(true);
        }
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

    private static int speed = 1;  // Speed of the movement
    private static int positionsAlreadyCalculated = 0;
    private static int resolution = 100;
    public static int selectedPoint = 0;

    private static bool isMoving = false;
    private static bool panelActive = false;
    public static bool currentlyDrawing = false;

    public static List<Vector3> positions = new();
    public static List<Quaternion> rotations = new();
    private static List<float> segmentLengths = new();
    private static List<float> segmentStarts = new();

    private static int lastFoundT = 0;
    private static float movedDistance = 0f;
    private static float totalMovedDistance = 0f;
    private static Vector3 lastPosition = Vector3.zero;
    private static int lastFoundSegment = 0;
    private static int delay = 0;

    private static List<List<Vector3>> segmentsCalculatedUsedPositions = new();
    private static List<List<Quaternion>> segmentsCalculatedUsedRotations = new();
    private static List<List<Vector3>> segmentsCalculatedPath = new();
    private static List<List<Quaternion>> segmentsCalculatedRotations = new();
    private static List<List<float>> segmentTValuesCalculated = new();
    private static List<List<float>> accumulatedLenghtsCalculated = new();
    private static List<List<Vector3>> finalPathCalculated = new();
    private static List<List<Quaternion>> finalRotationsCalculated = new();
    private static List<float> CalculatedSegmentLenghts = new();

    private static List<GameObject> cubes = new();
    private static List<SonsSdk.DebugTools.LineDrawer> lines = new();
    private static List<SonsSdk.DebugTools.LineDrawer> lineDrawers = new();
    
    
    private static Vector3 startPos = Vector3.zero;

    private static float tSegment = 0f;
    private static float totalLength = 0f;

    public static string fileLocation = $"Mods/CameraFlow/";

    //B-Splines
    public static void CalculatePath(bool force)
    {
        List<Vector3> positionsTemporary = new();
        List<Quaternion> rotationsTemporary = new();
        bool forceFullRecalculation = false;

        if (resolution != Config.Resolution.Value)
        {
            resolution = Config.Resolution.Value;
            forceFullRecalculation = true;
        }

        if (speed != Config.Speed.Value)
        {
            speed = Config.Speed.Value;
            forceFullRecalculation = true;
        }

        //destroy path before starting
        if (currentlyDrawing) { drawPath(false); }

        if (positions.Count < 4)  // Need at least 4 points for B-Splines
        {
            return;
        }

        //Create temporary lists to compare the new positions with the old ones 
        List<float> TempSegmentStarts = segmentStarts.ToList();

        clearCalculatedPaths();

        
        // Calculate the direction of the first segment
        Vector3 firstSegmentDirection = (positions[1] - positions[0]).normalized;

        // Calculate the position of the imaginary point
        Vector3 imaginaryPoint = positions[0] - firstSegmentDirection * 10;  // Adjust the multiplier as needed

        // Calculate the direction of the last segment
        Vector3 lastSegmentDirection = (positions[positions.Count - 1] - positions[positions.Count - 2]).normalized;

        // Calculate the position of the imaginary point
        Vector3 lastImaginaryPoint = positions[positions.Count - 1] + lastSegmentDirection * 10;  // Adjust the multiplier as needed

        
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
        int iteration = 0;
        float targetDistance = speed / 100f;
        float currentDistance = 0;
        bool checkedSegmentforChanges = false;
        float realMovedLength = 0;

        EnsureListCapacity(positionsTemporary.Count - 3);
        try {
            while (segmentIndex < positionsTemporary.Count - 3)
            {

                if (!checkedSegmentforChanges)
                {
                    // Check if this segment's control points have changed
                    List<Vector3> usedPositions = new() {
                        positionsTemporary[segmentIndex],
                        positionsTemporary[segmentIndex + 1],
                        positionsTemporary[segmentIndex + 2],
                        positionsTemporary[segmentIndex + 3]
                    };

                    List<Quaternion> usedRotations = new()
                    {
                        rotationsTemporary[segmentIndex],
                        rotationsTemporary[segmentIndex + 1],
                        rotationsTemporary[segmentIndex + 2],
                        rotationsTemporary[segmentIndex + 3]
                    };

                    bool segmentChanged = false;
                    
                    if (segmentsCalculatedUsedPositions.Count > segmentIndex)  // Ensure the index is valid
                    {
                        
                        // Compare each control point in usedPositions with the corresponding cached position
                        for (int i = 0; i < 4; i++)
                        {
                            if (segmentsCalculatedUsedPositions[segmentIndex].Count == 0)
                            {
                                segmentChanged = true;
                                break;
                            }

                            if (!usedPositions[i].Equals(segmentsCalculatedUsedPositions[segmentIndex][i]) || !usedRotations[i].Equals(segmentsCalculatedUsedRotations[segmentIndex][i]))
                            {
                                segmentChanged = true;
                                break; // No need to continue checking if one point is different
                            }
                        }
                        
                    }
                    else
                    {
                        // If the segment is new (not in the cache), it's considered changed
                        segmentChanged = true;
                    }

                    if (forceFullRecalculation)
                    {
                        segmentChanged = true;
                    }

                    if (segmentChanged) {
                        RLog.Msg("Segment " + segmentIndex + " changed");
                        checkedSegmentforChanges = true;

                        // Reset stored values for this segment since it has changed.
                        segmentsCalculatedPath[segmentIndex].Clear();
                        segmentsCalculatedRotations[segmentIndex].Clear();
                        accumulatedLenghtsCalculated[segmentIndex].Clear();
                        segmentTValuesCalculated[segmentIndex].Clear();
                        finalPathCalculated[segmentIndex].Clear();
                        finalRotationsCalculated[segmentIndex].Clear();

                    } else {
                        
                        //Not changed, use cached values
                        checkedSegmentforChanges = false;
                        segmentIndex++;
                        continue;
                    }
                }

                tSegment = Mathf.Clamp01(accumulatedLength / segmentLengths[segmentIndex]);

                // Calculate the position at this point on the curve
                Vector3 pointOnCurve = CalculateBSplinePoint(tSegment, 
                    positionsTemporary[segmentIndex], positionsTemporary[segmentIndex + 1], 
                    positionsTemporary[segmentIndex + 2], 
                    positionsTemporary[segmentIndex + 3]);
            

                Quaternion rotOnCurve = CalculateBSplineRotation(tSegment,
                    rotationsTemporary[segmentIndex],
                    rotationsTemporary[segmentIndex + 1],
                    rotationsTemporary[segmentIndex + 2],
                    rotationsTemporary[segmentIndex + 3]);

                segmentsCalculatedPath[segmentIndex].Add(pointOnCurve);
                segmentsCalculatedRotations[segmentIndex].Add(rotOnCurve);
                //add the lenght at the current position

                //if there are already two points in segmentsCalculatedPath we can calculate the distance between them
                float distanceMoved = 0f;
                if (segmentsCalculatedPath[segmentIndex].Count > 1)
                {
                    distanceMoved = Vector3.Distance(segmentsCalculatedPath[segmentIndex][segmentsCalculatedPath[segmentIndex].Count - 1], segmentsCalculatedPath[segmentIndex][segmentsCalculatedPath[segmentIndex].Count - 2]);
                    realMovedLength += distanceMoved;
                }

                //This is how far into the segment we are, so where this T value is found.
                accumulatedLenghtsCalculated[segmentIndex].Add(realMovedLength);
                //add the t value of the current position
                segmentTValuesCalculated[segmentIndex].Add(tSegment);

                accumulatedLength += spacing;

                // If we've moved beyond the current segment
                while (segmentIndex < positionsTemporary.Count - 3 && accumulatedLength > segmentLengths[segmentIndex])
                {
                    //add the current length of this segment to the segment List to later on find when a segment is being passed
                    CalculatedSegmentLenghts[segmentIndex] = accumulatedLength;
                    segmentStarts[segmentIndex] = realMovedLength;
                    // Subtract the length of the current segment and move on to the next one
                    accumulatedLength -= segmentLengths[segmentIndex];

                    List<Vector3> usedPositions = new() 
                    {
                        positionsTemporary[segmentIndex],
                        positionsTemporary[segmentIndex + 1],
                        positionsTemporary[segmentIndex + 2],
                        positionsTemporary[segmentIndex + 3]
                    };

                    List<Quaternion> usedRotations = new()
                    {
                        rotationsTemporary[segmentIndex],
                        rotationsTemporary[segmentIndex + 1],
                        rotationsTemporary[segmentIndex + 2],
                        rotationsTemporary[segmentIndex + 3]
                    };

                    segmentsCalculatedUsedPositions[segmentIndex] = usedPositions;
                    segmentsCalculatedUsedRotations[segmentIndex] = usedRotations;
                    realMovedLength = 0;

                    checkedSegmentforChanges = false;
                    segmentIndex++;
                    iteration = 0;
                }

                //Find the distance moved on this iteration and add it to the totalAccumulatedLength
                if (iteration != 0)
                {
                    currentDistance += distanceMoved;
                }
                else
                {
                    finalPathCalculated[segmentIndex].Add(pointOnCurve);
                    finalRotationsCalculated[segmentIndex].Add(rotOnCurve);
                }

                if (currentDistance >= targetDistance)
                {
                    finalPathCalculated[segmentIndex].Add(pointOnCurve);
                    finalRotationsCalculated[segmentIndex].Add(rotOnCurve);
                    currentDistance = 0;
                }

                iteration++;
            }




        } catch (Exception ex)
        {
            RLog.Error("An error occurred while calculating the path: " + ex.ToString());
        }
        
        positionsAlreadyCalculated = positions.Count;
        drawRefresh();
    }

    public static void EnsureListCapacity(int segmentIndex)
    {
        while (segmentsCalculatedUsedPositions.Count <= segmentIndex)
        {
            segmentsCalculatedUsedPositions.Add(new List<Vector3>());
            segmentsCalculatedUsedRotations.Add(new List<Quaternion>());
            segmentsCalculatedPath.Add(new List<Vector3>());
            segmentsCalculatedRotations.Add(new List<Quaternion>());
            accumulatedLenghtsCalculated.Add(new List<float>());
            segmentTValuesCalculated.Add(new List<float>());
            finalPathCalculated.Add(new List<Vector3>());
            finalRotationsCalculated.Add(new List<Quaternion>());
            CalculatedSegmentLenghts.Add(0);
            segmentStarts.Add(0);
        }
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
        if (speed != Config.Speed.Value)
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
        else 
        {
            if (panelActive) MenuToggle(); 
            CalculatePath(false);
            SetCameraMovementSittingFix(false);
            LocalPlayer.RaceSystem.SwitchToThirdPerson();
            ToggleFreeCamera(true);
            toggleUI(false);
            ToggleGodMode(true);
            SetCameraMovement(false);

            lastFoundT = 0;
            movedDistance = 0f;
            totalMovedDistance = 0f;
            lastFoundSegment = 0;

            // Set the camera's position to the first position in the list
            GameObject freeCam = GameObject.Find("MainCameraFP");
            if (positions.Count > 0)
            {
                freeCam.transform.position = positions[0];
                freeCam.transform.rotation = rotations[0];
                delay = Config.Delay.Value;
            }
            isMoving = true;
        }
    }

    private static void clearCalculatedPaths()
    { 
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

        if(delay > 0)
        {
            delay--;
        }

    }

  

    public void moveCamera()
    {

        if (!isMoving || delay > 0)
        {
            return;
        }

        List<Vector3> positionsTemporary = new();
        List<Quaternion> rotationsTemporary = new();


        // Calculate the direction of the first segment
        Vector3 firstSegmentDirection = (positions[1] - positions[0]).normalized;

        // Calculate the position of the imaginary point
        Vector3 imaginaryPoint = positions[0] - firstSegmentDirection * 10;  // Adjust the multiplier as needed

        // Calculate the direction of the last segment
        Vector3 lastSegmentDirection = (positions[positions.Count - 1] - positions[positions.Count - 2]).normalized;

        // Calculate the position of the imaginary point
        Vector3 lastImaginaryPoint = positions[positions.Count - 1] + lastSegmentDirection * 10;  // Adjust the multiplier as needed
        
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
        
        GameObject freeCam = GameObject.Find("MainCameraFP");

        // Calculate target distance for this frame
        float fixedTimeStep = 0.0166667f; // 60 FPS
        float distanceIncrement = (speed / 10) * fixedTimeStep;
        movedDistance += distanceIncrement * (Time.deltaTime / fixedTimeStep);
        totalMovedDistance += distanceIncrement * (Time.deltaTime / fixedTimeStep);
        float tempStepSize = distanceIncrement * (Time.deltaTime / fixedTimeStep);
        float desiredDistance = distanceIncrement * (Time.deltaTime / fixedTimeStep);
        int targetIndex = lastFoundSegment;

        if(movedDistance >= segmentStarts[targetIndex])
        { 
            movedDistance -= segmentStarts[targetIndex];
            targetIndex += 1;
            lastFoundT = 0;
        }

        // Calculate T value within the current segment, start with the search at the last found index to speed up the search
        int tIndex = FindIndexForDistance(movedDistance, accumulatedLenghtsCalculated[targetIndex], lastFoundT);
        float T = segmentTValuesCalculated[targetIndex][tIndex];
        lastFoundT = tIndex;



        if (totalMovedDistance >= totalLength)
        {
            // Stop movement
            StopMoving();
            SonsTools.ShowMessage("Camera flow ended");
            return; // Exit the function early
        }

        // Evaluate B-spline and move the object
        Vector3 targetPoint = CalculateBSplinePoint(T,
                                                    positionsTemporary[targetIndex],
                                                    positionsTemporary[targetIndex + 1],
                                                    positionsTemporary[targetIndex + 2],
                                                    positionsTemporary[targetIndex + 3]);

        freeCam.transform.position = targetPoint;

        Quaternion targetRotation = CalculateBSplineRotation(T,
                                                    rotationsTemporary[targetIndex],
                                                    rotationsTemporary[targetIndex + 1],
                                                    rotationsTemporary[targetIndex + 2],
                                                    rotationsTemporary[targetIndex + 3]);
        
        float distanceToLast = Vector3.Distance(lastPosition, targetPoint);

        // Set the camera's rotation
        freeCam.transform.rotation = targetRotation;

        if (Mathf.Abs(distanceToLast - desiredDistance) > 0.005 && isMoving)
        {
            RLog.Error("Step too big: " + desiredDistance + " vs " + distanceToLast);
            RLog.Msg("Moved to position " + totalMovedDistance + " of " + totalLength + " \nFound T " + T + " at " + accumulatedLenghtsCalculated[targetIndex][tIndex] + " \nCurrent Segment: " + targetIndex + " with " + movedDistance + " of total " + segmentStarts[targetIndex]);
        }

        lastPosition = targetPoint;
        lastFoundSegment = targetIndex;
    }

    // Helper method for search
    private static int FindIndexForDistance(float targetDistance, List<float> accumulatedDistances, int startFrom)
    {
        // Iterate from 0 upwards until we find the target distance or exceed it
        for (int i = startFrom; i < accumulatedDistances.Count; i++)
        {
            if (accumulatedDistances[i] >= targetDistance)
            {
                return i;
            }
        }

        // Handle the case where we reach the end of the list without finding the distance
        return accumulatedDistances.Count - 1; // Return the last index
    }


    public static void drawPath(bool drawOrClear)
    {
        if (finalPathCalculated.Count > 0)
        {
            if (drawOrClear)
            {

                List<Vector3> pathPointsCalculated = new();

                for (int i = 0; i < finalPathCalculated.Count; i++)
                {
                    for (int j = 0; j < finalPathCalculated[i].Count; j++)
                    {
                        pathPointsCalculated.Add(finalPathCalculated[i][j]);
                    }
                    
                }

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
        //check if camera is moving
        if (isMoving) return;

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

    public static string SaveCameraFlowData(string fileName)
    {
        try
        {
            if (positions.Count == 0)
            {
                SonsTools.ShowMessage("No path found!");
                return null;
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
            return path;
        }
        catch (Exception ex)
        {
            // Log the exception
            RLog.Error("An error occurred while saving camera flow data: " + ex.ToString());
            return null;
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
            RLog.Error("An error occured while loading camera flow data: " + ex.ToString());
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
                SonsTools.ShowMessage(fileName + " deleted successfully!");
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

        RLog.Msg("Segment Lengths: " + segmentLengths.Count);
        RLog.Msg("Total Length: " + totalLength);

        RLog.Error("End Debugging Camera Flow");

        for (int i = 0; i < segmentStarts.Count; i++)
        {
            RLog.Msg("Segment " + i + " " + segmentStarts[i] + " Segment Position Count " + accumulatedLenghtsCalculated[i].Count + " T " + segmentTValuesCalculated[i].Count);
        }
    }


    //SUI Bullshittery

    public void MenuToggle()
    {
        var panel = GetPanel("SidePanel");
        panelActive = !panelActive;
        if (panelActive)
        {
            if (isMoving) return;
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