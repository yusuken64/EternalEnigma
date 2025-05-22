using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CheatConsole : MonoBehaviour
{
    public GameObject ScreenObject;
    public TMPro.TMP_InputField InputField;
    public TextMeshProUGUI ConsoleLog;

    private delegate void CommandHandler(string[] args);
    private Dictionary<string, CommandHandler> commandMap;

    private void Start()
    {
        ConsoleLog.text = "";
        ScreenObject.gameObject.SetActive(false);
        commandMap = new Dictionary<string, CommandHandler>(StringComparer.OrdinalIgnoreCase)
        {
            { "gold", HandleGold },
            { "level", HandleLevel },
            //{ "heal", HandleHeal },
            //{ "spawn", HandleSpawn }
        };
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

        //if (InputField.isFocused && Input.GetKeyDown(KeyCode.Return)))
        //{
        //    OnSubmit(InputField.text);
        //}
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

        if (commandMap.TryGetValue(command, out var handler))
        {
            try
            {
                handler.Invoke(args);
                ConsoleLog.text += Environment.NewLine + $"{command}: {string.Join(',', args)}";
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
    }

    public void HandleGold(string[] args)
    {
        if (args.Length < 1 || !int.TryParse(args[0], out int amount))
        {
            ConsoleLog.text += Environment.NewLine + "Usage: gold <amount>";
            return;
        }

        var player = FindFirstObjectByType<OverworldPlayer>();
        if (player != null)
        {
            player.Gold += amount;
            ConsoleLog.text += Environment.NewLine + $"Added {amount} gold to player.";
        }
        else
        {
            ConsoleLog.text += Environment.NewLine + "No player found.";
        }
    }

    private void HandleLevel(string[] args)
    {
        int startingFloor = 0; // default to floor 0

        if (args.Length >= 1 && !int.TryParse(args[0], out startingFloor))
        {
            Debug.LogWarning($"Invalid floor argument '{args[0]}', defaulting to floor 0.");
            startingFloor = 0;
        }

        Common.Instance.GameSaveData.StartingFloor = startingFloor;
        SceneManager.LoadScene("DungeonScene");
    }
}
