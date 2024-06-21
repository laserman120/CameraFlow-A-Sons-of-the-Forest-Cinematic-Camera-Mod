namespace CameraFlow;
using SonsSdk;
using SUI;
using UnityEngine;
using static SUI.SUI;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

public class CameraFlowUi
{
    private static readonly Observable<string> selectedPoint = new("");
    private static readonly Observable<int> positionsCount = new(0);

    //static Observable<float> posX = new(0);
    //static Observable<float> rotX = new(0);
    private static readonly Observable<string> posXString = new("");
    private static readonly Observable<string> rotXString = new("");
    private static readonly Observable<string> posYString = new("");
    private static readonly Observable<string> rotYString = new("");
    private static readonly Observable<string> posZString = new("");
    private static readonly Observable<string> rotZString = new("");
    private static readonly Observable<bool> easing = new(new bool());
    private static readonly Observable<float> easingMultiplier = new(1f);

    private static readonly int fontSize = 10;
    private static float moveAmount = 0.5f;
    private static string fileName = "";

    public static void Create()
    {
        // Ui creation code goes here
        var sidePanel = RegisterNewPanel("SidePanel", true) // registering the panel and enabling input listening
        .Pivot(0, 1) // setting the right pivot for top left corner of the screen (refer to Panels lesson if you don't udnerstand)
        .Anchor(AnchorType.TopLeft)
        .Background(Color.black, EBackground.Sons)// changing panel color to red and styling it with straight borders
        .Opacity(0.5f) //Opacity of the panel
        .Size(500) // specifying the width of the panel (the height can be omitted)
        .Vertical(5, "EC") // specifying that the panel must have a vertical layout (so it's content is distributed vertically)
        .VFill(); // specifying that the panel must fill the screen vertically

        // creating the scrollbar?
        var scrollBar = SDiv.FlexHeight(1);
        _ = sidePanel.Add(scrollBar);

        // creating the container which will be inside the main panel
        var settingsScroll = SScrollContainer // declaring the container as Scrollable
        .Dock(EDockType.Fill) // specifying that the container will fill all of the available space
        .Background(Color.grey.WithAlpha(0f), EBackground.None)
        .Opacity(Config.Opacity.Value) // setting the opacity of the container
        .Size(-20, -20); // adding some padding from the main container

        // further personalizing the panel to make it look better
        _ = settingsScroll.Spacing(80); // the space between each vertical ui element
        _ = settingsScroll.PaddingHorizontal(40); // the space on left and right from the scrollable container towards the ui elements
        _ = settingsScroll.PaddingVertical(40); // the space on top and bottom from the scrollable container towards the ui elements
        _ = scrollBar.Add(settingsScroll); // adding the container to the scrollbar element

        CreateDivider(settingsScroll, "Selected Point");

        //First Container To display which  point is currently being edited
        var titleContainer = SDiv.FlexHeight(1)
        .Background(Color.black, EBackground.None)
        .Size(-20, 50)
        .Horizontal(5, "EC");
        _ = settingsScroll.Add(titleContainer);

        var previousButtonContainer = SContainer.Background(Color.gray, EBackground.RoundOutline).Anchor(AnchorType.Fill).Size(20, 20).Vertical()
        - SBgButton.Text("<-")
        .Size(20, 20)
        .FontColor(Color.white)
        .Color(Color.black.WithAlpha(0f))
        .Anchor(AnchorType.Fill)
            .OnClick(() =>
            {
                if (CameraFlow.selectedPoint > 0)
                {
                    CameraFlow.selectedPoint -= 1;
                }

                if (CameraFlow.currentlyDrawing)
                {
                    CameraFlow.drawPath(false);
                    CameraFlow.drawPath(true);
                }
            });
        _ = titleContainer.Add(previousButtonContainer);

        var titleTextContainer = SContainer.Background(Color.gray, EBackground.RoundOutline).Anchor(AnchorType.Fill).Size(20, 20).Vertical()
        - SLabel.Text(selectedPoint.Value)
        .Size(20, 20)
        .FontColor(Color.white)
        .Anchor(AnchorType.Fill)
        .Bind(selectedPoint);
        _ = titleContainer.Add(titleTextContainer);

        var nextButtonContainer = SContainer.Background(Color.gray, EBackground.RoundOutline).Anchor(AnchorType.Fill).Size(20, 20).Vertical()
        - SBgButton.Text("->")
        .FontColor(Color.white)
        .Color(Color.black.WithAlpha(0f))
        .Anchor(AnchorType.Fill)
        .Background(EBackground.None)
        .Size(20, 20)
        .OnClick(() =>
            {
                if (CameraFlow.selectedPoint < CameraFlow.positions.Count - 1)
                {
                    CameraFlow.selectedPoint += 1;
                }

                if (CameraFlow.currentlyDrawing)
                {
                    CameraFlow.drawPath(false);
                    CameraFlow.drawPath(true);
                }
            });
        _ = titleContainer.Add(nextButtonContainer);

        CreateDivider(settingsScroll, "Move Point");

        CreatePositionContainer(settingsScroll, "X", posXString, (amount, minus) => updatePosition("X", amount, minus), Color.red);
        CreatePositionContainer(settingsScroll, "Y", posYString, (amount, minus) => updatePosition("Y", amount, minus), Color.green);
        CreatePositionContainer(settingsScroll, "Z", posZString, (amount, minus) => updatePosition("Z", amount, minus), Color.blue);

        CreateDivider(settingsScroll, "Rotate Point");

        CreatePositionContainer(settingsScroll, "Down", rotXString, (amount, minus) => updateRotation("X", amount, minus), Color.red);
        CreatePositionContainer(settingsScroll, "Right", rotYString, (amount, minus) => updateRotation("Y", amount, minus), Color.green);

        CreateDivider(settingsScroll, "Adjust Amount");

        //AMOUNT SLIDER
        var amountSliderContainer = SContainer.Background(Color.black.WithAlpha(0f), EBackground.ShadowPanel).Size(-20, 40)
        - SSlider.Text("").Notify(PrintSlider)
        .Position(210, -25)
        .Size(475, 60)

        .InputFlexWidth(50f)
        .Background(EBackground.None)
        .Value(1f)
        .Range(0.1f, 5f)
        .Format("0.0")
        .Step(0.2f);

        _ = settingsScroll.Add(amountSliderContainer);

        //DELETE POINT

        CreateDivider(settingsScroll, "Delete Point");

        var deleteButtonContainer = SContainer.Background(Color.red, EBackground.RoundOutline).Size(-20, 40).Vertical()
        - SBgButton.Text("Delete Point")
        .FontColor(Color.red)
        .Color(Color.black.WithAlpha(0f))
        .Anchor(AnchorType.Fill)
        .Background(EBackground.None)
        .Size(20, 20)
        .OnClick(() =>
        {
            //before deleting check if there are any points left
            if (CameraFlow.positions.Count == 0)
            {
                SonsTools.ShowMessage("No Points to delete");
                return;
            }
            SonsTools.ShowMessage("Deleting Point " + selectedPoint.Value + "...");
            destroyPoint(CameraFlow.selectedPoint);
            if(CameraFlow.selectedPoint == CameraFlow.positions.Count)
            {
                CameraFlow.selectedPoint -= 1;
            }
            CameraFlow.CalculatePath();
        });
        _ = settingsScroll.Add(deleteButtonContainer);

        CreateDivider(settingsScroll, "Insert Point");


        var addPointButtonContainer = SContainer.Background(Color.gray, EBackground.RoundOutline).Size(-20, 40).Horizontal()
            - SBgButton.Text("Add Point Before")
            .FontColor(Color.white)
            .Color(Color.black.WithAlpha(0f))
            .Anchor(AnchorType.Fill)
            .Background(EBackground.None)
            .OnClick(() => addPoint(false))
            - SBgButton.Text("Add Point After")
            .FontColor(Color.white)
            .Color(Color.black.WithAlpha(0f))
            .Anchor(AnchorType.Fill)
            .Background(EBackground.None)
            .OnClick(() => addPoint(true));
        _ = settingsScroll.Add(addPointButtonContainer);

        CreateDivider(settingsScroll, "Reset the Path");

        //Delete path container
        var deletePathContainer = SContainer.Background(Color.red, EBackground.RoundOutline).Size(-20, 40).Vertical()
            - SBgButton.Text("Delete All Points")
            .FontColor(Color.red)
            .Color(Color.black.WithAlpha(0f))
            .Anchor(AnchorType.Fill)
            .Background(EBackground.None)
            .OnClick(() =>
            {
                SonsTools.ShowMessage("Deleting All Points...");
                CameraFlow.clearCalculatedPaths();
            });
        _ = settingsScroll.Add(deletePathContainer);

        //save path container
        CreateDivider(settingsScroll, "Save current Path to file");

        var savePathContainer = SContainer.Background(Color.gray, EBackground.RoundOutline).Size(-20, 40).Horizontal().Anchor(AnchorType.Fill).PaddingHorizontal(10)
            - STextbox.Text("Filename")
            .InputFlexWidth(5f)
            .Anchor(AnchorType.Fill)
            .VFill()
            .Notify(recieveNameChange)
            - SBgButton.Text("Save")
            .FontColor(Color.green)
            .Color(Color.black.WithAlpha(0f))
            .Anchor(AnchorType.Fill)
            .Background(EBackground.None)
            .OnClick(() =>
            {
                SonsTools.ShowMessage("Saving Path...");
                var path = CameraFlow.SaveCameraFlowData(fileName);
                if (path != null)
                {
                    createLoadPathContainer(settingsScroll, fileName, path);
                }

            });
        _ = settingsScroll.Add(savePathContainer);

        CreateDivider(settingsScroll, "Load a saved Path");

        var files = ListJsonFiles();

        foreach (var file in files)
        {
            if (Path.GetFileNameWithoutExtension(file) == "manifest")
            {
                continue;
            }

            createLoadPathContainer(settingsScroll, Path.GetFileNameWithoutExtension(file), file);
        }
    }

    public static void CreateDivider(SContainerOptions settingsScroll, string text)
    {
        var divider = SContainer.Background(Color.black.WithAlpha(0f), EBackground.RoundOutline).Size(-20, -20).Horizontal().Anchor(AnchorType.Fill).PaddingHorizontal(10)
        - SLabelDivider.Text(text).FontSize(fontSize).Anchor(AnchorType.Fill).Size(-10, -10);
        _ = settingsScroll.Add(divider);
    }

    public static string[] ListJsonFiles()
    {
        // Get the path to the directory
        var path = CameraFlow.fileLocation;

        // Get all the JSON files in the directory
        var files = Directory.GetFiles(path, "*.json");

        return files;
    }

    private static void recieveNameChange(string name) => fileName = name;

    private static void createLoadPathContainer(SContainerOptions settingsScroll, string label, string path)
    {
        var size = 10;

        var loadPathContainer = SContainer.Background(Color.gray, EBackground.RoundOutline).Size(-20, 40).Horizontal(2, "EC");
        _ = settingsScroll.Add(loadPathContainer);

        var labelContainer = SContainer.Background(Color.gray.WithAlpha(0f), EBackground.None).Size(-20, -30).Vertical().PWidth(260).MWidth(260)
        - SLabel.Text(label)
        .FontColor(Color.white)
        .Anchor(AnchorType.Fill);
        _ = loadPathContainer.Add(labelContainer);


        var loadButtonContainer = SContainer.Background(Color.gray, EBackground.RoundOutline).Size(-10, -20).Vertical().PWidth(size).MWidth(size).Anchor(AnchorType.MiddleRight)
            - SBgButton.Text("Load")
            .FontColor(Color.green)
            .Color(Color.black.WithAlpha(0f))
            .Anchor(AnchorType.Fill)
            .Background(EBackground.RoundOutline)
            .OnClick(() =>
            {
                SonsTools.ShowMessage("Loading Path...");
                CameraFlow.LoadCameraFlowData(path);
            });
        _ = loadPathContainer.Add(loadButtonContainer);

        var deleteButtonContainer = SContainer.Background(Color.gray, EBackground.RoundOutline).Size(-10, -20).Vertical().PWidth(size).MWidth(size).Anchor(AnchorType.MiddleRight)
            - SBgButton.Text("Delete")
            .FontColor(Color.red)
            .Color(Color.black.WithAlpha(0f))
            .Anchor(AnchorType.Fill)
            .Background(EBackground.RoundOutline)
            .OnClick(() =>
            {
                SonsTools.ShowMessage("Deleting Path...");
                CameraFlow.DeleteCameraFlowData(path);
                loadPathContainer.Remove();
            });
        _ = loadPathContainer.Add(deleteButtonContainer);
    }


    private static void CreatePositionContainer(SContainerOptions settingsScroll, string label, Observable<string> binding, Action<float, bool> onClickAction, Color color)

    {
        var size = 10;

        var posContainer = SDiv.FlexHeight(1)
           .Background(Color.black, EBackground.None)
           .Size(-20, 40)
           .Horizontal(5, "EC");
        _ = settingsScroll.Add(posContainer);

        var labelContainer = SContainer.Background(color, EBackground.RoundOutline).Size(-20, -30).Vertical().PWidth(size).MWidth(size)
        - SLabel.Text(label)
        .FontColor(Color.white)
        .Anchor(AnchorType.Fill);
        _ = posContainer.Add(labelContainer);

        var minusButtonContainer = SContainer.Background(Color.gray, EBackground.RoundOutline).Size(-10, -20).Vertical().PWidth(size).MWidth(size)
        - SBgButton.Text("-")
        .FontColor(Color.white)
        .Color(Color.black.WithAlpha(0f))
        .Anchor(AnchorType.Fill)
        //.Background(EBackground.None)
        .OnClick(() => onClickAction(moveAmount, true));
        _ = posContainer.Add(minusButtonContainer);

        var valueContainer = SContainer.Background(Color.gray, EBackground.RoundOutline).Size(-20, -30).Vertical()
        - SLabel.Text(selectedPoint.Value)
        .FontColor(Color.white)
        .Anchor(AnchorType.Fill)
        .Bind(binding);
        _ = posContainer.Add(valueContainer);

        var plusButtonContainer = SContainer.Background(Color.gray, EBackground.RoundOutline).Size(-20, -40).Vertical().PWidth(size).MWidth(size)
        - SBgButton.Text("+")
        .FontColor(Color.white)
        .Color(Color.black.WithAlpha(0f))
        .Anchor(AnchorType.Fill)
        .OnClick(() => onClickAction(moveAmount, false));
        _ = posContainer.Add(plusButtonContainer);
    }

    public static void destroyPoint(int point)
    {
        CameraFlow.positions.RemoveAt(point);
        CameraFlow.rotations.RemoveAt(point);
        CameraFlow.CalculatePath();
    }

    public static void addPoint(bool after)
    {
        if ((!after && CameraFlow.selectedPoint < CameraFlow.positions.Count) || (after && CameraFlow.selectedPoint != 0))
        {
            var selectedPosition = CameraFlow.positions[CameraFlow.selectedPoint];
            var selectedRotation = CameraFlow.rotations[CameraFlow.selectedPoint];

            _ = Vector3.zero;

            _ = Quaternion.identity;

            Vector3 secondPosition;

            Quaternion secondRotation;
            if (after)
            {
                secondPosition = CameraFlow.positions[CameraFlow.selectedPoint + 1];
                secondRotation = CameraFlow.rotations[CameraFlow.selectedPoint + 1];
            }
            else
            {
                secondPosition = CameraFlow.positions[CameraFlow.selectedPoint - 1];
                secondRotation = CameraFlow.rotations[CameraFlow.selectedPoint - 1];
            }
            //calculate the position and rotation of the new point between the selected point and the next point
            var newPosition = (selectedPosition + secondPosition) / 2;
            var newRotation = Quaternion.Lerp(selectedRotation, secondRotation, 0.5f);

            //insert into the list before or after the selected point
            if (after)
            {
                CameraFlow.positions.Insert(CameraFlow.selectedPoint + 1, newPosition);
                CameraFlow.rotations.Insert(CameraFlow.selectedPoint + 1, newRotation);
            }
            else
            {
                CameraFlow.positions.Insert(CameraFlow.selectedPoint, newPosition);
                CameraFlow.rotations.Insert(CameraFlow.selectedPoint, newRotation);
            }
            CameraFlow.CalculatePath();
        }
    }

    public static void PrintSlider(float value) => moveAmount = value;

    public static void Update()
    {
        selectedPoint.Value = CameraFlow.selectedPoint.ToString();
        positionsCount.Value = CameraFlow.positions.Count;
        if (CameraFlow.selectedPoint < CameraFlow.positions.Count)
        {
            posXString.Value = CameraFlow.positions[CameraFlow.selectedPoint].x.ToString();
            posYString.Value = CameraFlow.positions[CameraFlow.selectedPoint].y.ToString();
            posZString.Value = CameraFlow.positions[CameraFlow.selectedPoint].z.ToString();

            rotXString.Value = CameraFlow.rotations[CameraFlow.selectedPoint].x.ToString();
            rotYString.Value = CameraFlow.rotations[CameraFlow.selectedPoint].y.ToString();
            rotZString.Value = CameraFlow.rotations[CameraFlow.selectedPoint].z.ToString();

            //limit the amount of decimals to two
            posXString.Value = posXString.Value[..Mathf.Min(5, posXString.Value.Length)];
            posYString.Value = posYString.Value[..Mathf.Min(5, posYString.Value.Length)];
            posZString.Value = posZString.Value[..Mathf.Min(5, posZString.Value.Length)];

            rotXString.Value = rotXString.Value[..Mathf.Min(5, rotXString.Value.Length)];
            rotYString.Value = rotYString.Value[..Mathf.Min(5, rotYString.Value.Length)];
            rotZString.Value = rotZString.Value[..Mathf.Min(5, rotZString.Value.Length)];

        }
        else
        {
            easing.Value = false;
            easingMultiplier.Value = 1f;
        }

    }

    public static void updatePosition(string XYZ, float amount, bool minus)
    {
        if (CameraFlow.selectedPoint < CameraFlow.positions.Count)
        {
            if (minus)
            {
                amount = -amount;
            }
            // Get the Vector3 from the list
            var position = CameraFlow.positions[CameraFlow.selectedPoint];
            switch (XYZ)
            {
                case "X":
                    // Modify the Vector3
                    position.x += amount;
                    break;
                case "Y":
                    position.y += amount;
                    break;
                case "Z":
                    position.z += amount;
                    break;
                default:
                    break;
            }
            // Put the Vector3 back in the list
            CameraFlow.positions[CameraFlow.selectedPoint] = position;

            CameraFlow.CalculatePath();
        }
    }

    public static void updateRotation(string XYZ, float amount, bool minus)
    {
        if (CameraFlow.selectedPoint < CameraFlow.rotations.Count)
        {
            if (minus)
            {
                amount = -amount;
            }
            // Get the Quaternion from the list
            var rotation = CameraFlow.rotations[CameraFlow.selectedPoint];

            // Convert the Quaternion to Euler angles
            var euler = rotation.eulerAngles;

            // Apply the rotation
            switch (XYZ)
            {
                case "X":
                    euler.x += amount;
                    break;
                case "Y":
                    euler.y += amount;
                    break;
                case "Z":
                    euler.z += amount;
                    break;
                default:
                    throw new ArgumentException($"Invalid rotation axis: {XYZ}");
            }

            // Convert the Euler angles back to a Quaternion
            rotation = Quaternion.Euler(euler);

            // Put the Quaternion back in the list
            CameraFlow.rotations[CameraFlow.selectedPoint] = rotation;

            CameraFlow.CalculatePath();
        }
    }

}
