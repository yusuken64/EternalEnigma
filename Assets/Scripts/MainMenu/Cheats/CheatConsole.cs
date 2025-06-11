using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CheatConsole : MonoBehaviour
{
    public GameObject ScreenObject;
    public TMP_InputField InputField;
    public TextMeshProUGUI ConsoleLog;

    private Dictionary<string, IConsoleCommand> commandMap;

    private void Start()
    {
        ConsoleLog.text = "";
        ScreenObject.gameObject.SetActive(false);

        commandMap = new Dictionary<string, IConsoleCommand>(StringComparer.OrdinalIgnoreCase);

        RegisterCommand(new GoldCommand());
        RegisterCommand(new FloorCommand());
        RegisterCommand(new ItemCommand());
        RegisterCommand(new ItemsCommand());
        RegisterCommand(new VitalsCommand());
        RegisterCommand(new AllyCommand());
    }

    private void RegisterCommand(IConsoleCommand command)
    {
        commandMap[command.Name] = command;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tilde) ||
            Input.GetKeyDown(KeyCode.BackQuote))
        {
            ScreenObject.gameObject.SetActive(!ScreenObject.activeSelf);
            if (ScreenObject.gameObject.activeSelf)
            {
                //reset input and focus
                InputField.text = string.Empty;
                InputField.Select();
                InputField.ActivateInputField();
            }
        }
    }

    public void OnSubmit(string text)
    {
        Debug.Log("Submitted with Enter: " + text);
        ConsoleLog.text += Environment.NewLine + text;

        var parts = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return;

        var command = parts[0];
        var args = parts.Skip(1).ToArray();

        if (commandMap.TryGetValue(command, out var cmd))
        {
            try
            {
                cmd.Execute(args, this);
            }
            catch (Exception ex)
            {
                ConsoleLog.text += Environment.NewLine + $"Error: {ex.Message}";
            }
        }
        else
        {
            ConsoleLog.text += Environment.NewLine + "Unknown command.";
        }

        InputField.text = string.Empty;
        InputField.Select();
        InputField.ActivateInputField();
    }

    internal void Log(string message)
    {
        ConsoleLog.text += Environment.NewLine + message;
    }

    internal OverworldPlayer FindOverworldPlayer()
    {
        return FindFirstObjectByType<OverworldPlayer>();
    }
    internal Ally FindPlayer()
    {
        return FindFirstObjectByType<PlayerController>().ControlledAlly;
    }
}
