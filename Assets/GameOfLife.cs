using System;
using UnityEngine;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;

public class GameOfLife : MonoBehaviour {

	public KMBombInfo Info;
	public KMBombModule Module;
	public KMAudio Audio;

	public KMSelectable[] Btn;
	public KMSelectable Clear;
	public KMSelectable Reset;
	public KMSelectable Submit;
	public MeshRenderer[] BtnColor;
	public Color32[] Colors;

	private int[] BtnColor1init = new int[48];
	private int[] BtnColor2init = new int[48];
	private int[] BtnColor1 = new int[48];
	private int[] BtnColor2 = new int[48];
	private int[] nCount = new int[48];
	private Color32[] ColorsSubmitted = new Color32[48];
	private Color32[] BtnColorStore = new Color32[48];

	private int BlackAmount = 34;
	//private int WhiteAmount = 14;
	private float TimeSneak = 0.4f;		// time the correct solution is displayed at a strike
	private float TimeTiny = 0.01f;		// time to allow computations in correct order. set to as low as possible

	private bool isActive = false;
	private bool isSolved = false;
	private bool isSubmitting = false;

	private static int moduleIdCounter = 1;
	private int moduleId = 0;


	/////////////////////////////////////////////////// Initial Setup ///////////////////////////////////////////////////////

	// Loading screen
	void Start () {

		moduleId = moduleIdCounter++;
		Module.OnActivate += Activate;
	}

	// Lights off
	void Awake () {
		//run initial setup
		InitSetup ();

		//assign button presses
		Clear.OnInteract += delegate ()
		{
			Audio.PlayGameSoundAtTransform (KMSoundOverride.SoundEffect.ButtonPress, Clear.transform);
			Clear.AddInteractionPunch ();

			if (isActive && !isSolved && !isSubmitting)
			{
				for (int i = 0; i < 48; i++)
				{
					BtnColor1[i] = 0;
					BtnColor2[i] = 0;
				}
				updateSquares();
			}
			return false;
		};

		Reset.OnInteract += delegate () {
			Audio.PlayGameSoundAtTransform (KMSoundOverride.SoundEffect.ButtonPress, Reset.transform);
			Reset.AddInteractionPunch ();
			if (isActive && !isSolved && !isSubmitting) {
				updateReset ();
			}
			return false;
		};

		Submit.OnInteract += delegate () {
			Audio.PlayGameSoundAtTransform (KMSoundOverride.SoundEffect.ButtonPress, Submit.transform);
			Submit.AddInteractionPunch ();
			if (isActive && !isSolved && !isSubmitting) {
				StartCoroutine (handleSubmit ());
			}
			return false;
		};
			
		for (int i = 0; i < 48; i++)
		{
			int j = i;
			Btn[i].OnInteract += delegate () {
				Audio.PlayGameSoundAtTransform (KMSoundOverride.SoundEffect.ButtonPress, Btn[j].transform);
				if (isActive && !isSolved && !isSubmitting) {
					handleSquare (j);
				}
				return false;
			};
		}
	}

	// Lights on
	void Activate () {
		Log("Cell color reference:\n◼ = Black\n◻ = White");
		
		updateDebug("Initial state");

		updateSquares();

		simulateGeneration();
		updateDebug("Solution");
		updateReset();

		isActive = true;
	}

	// Initial setup
	void InitSetup () {
		for (int i = 0; i < 48; i++)
		{
			// radomizing starting squares
			int x = Random.Range (0, 48);
			if (x < BlackAmount) {		// black, black
				BtnColor1[i] = BtnColor1init[i] = 0;
				BtnColor2[i] = BtnColor2init[i] = 0;
			} else {		// white, white
				BtnColor1 [i] = BtnColor1init[i] = 1;
				BtnColor2 [i] = BtnColor2init[i] = 1;
			}
		}
	}

	// Log function
	void Log(string message, params object[] args)
	{
		Debug.LogFormat("[Game of Life Simple #{0}] {1}", moduleId, string.Format(message, args));
	}

	/////////////////////////////////////////////////// Updates ///////////////////////////////////////////////////////

	// update the squares to correct colors
	private void updateSquares () {

		for (int i = 0; i < 48; i++) {
			int j = i;
			if (BtnColor1 [i] == 0 && BtnColor2 [i] == 0) {					// if both are black
				BtnColor [j].material.color = Colors [BtnColor1 [j]];
			} else {
				if (BtnColor1 [i] == 1 && BtnColor2 [i] == 1) {					// if both are white
					BtnColor [j].material.color = Colors [BtnColor1 [j]];
				} else {															// all other cases
					if (BtnColor [i].material.color == Colors [BtnColor1 [i]]) {
						BtnColor [j].material.color = Colors [BtnColor2 [j]];
					} else {
						BtnColor [j].material.color = Colors [BtnColor1 [j]];
					}
				}
			}
		}
	}

	// perform a reset to initial state
	private void updateReset () {
		for (int r = 0; r < 48; r++) {
			BtnColor1 [r] = BtnColor1init [r];
			BtnColor2 [r] = BtnColor2init [r];
		}
		updateSquares();
	}

	// display current state in debug log
	private void updateDebug(string title = "State")
	{
		string logString = title + ":\n";
		for (int d = 0; d < 48; d++)
		{
			if (BtnColor1[d] == 0 && BtnColor2[d] == 0)
			{
				logString += "◼";
			}
			else if (BtnColor1[d] == 1 && BtnColor2[d] == 1)
			{
				logString += "◻";
			}

			if ((d + 1) % 6 == 0) logString += "\n";
		}

		if (Application.isEditor) // Unity doesn't show the characters in t
		{
			Log("{0}", logString.Replace("◼", "B").Replace("◻", "W"));
		}
		else
		{
			Log("{0}", logString);
		}
	}

	void simulateGeneration()
	{
		// process the generation
		// store square color value
		for (int s = 0; s < 48; s++)
		{
			BtnColorStore[s] = BtnColor[s].material.color;
		}

		// process neighbours for each square
		for (int k = 0; k < 48; k++)
		{
			int l = k;
			nCount[l] = 0;
			// top left
			if ((k - 7 < 0) || (k % 6 == 0))
			{
			}
			else
			{
				if (BtnColorStore[(k - 7)].Equals(Colors[1]))
				{
					nCount[l]++;
				}
			}
			// top
			if (k - 6 < 0)
			{
			}
			else
			{
				if (BtnColorStore[(k - 6)].Equals(Colors[1]))
				{
					nCount[l]++;
				}
			}
			// top right
			if ((k - 5 < 0) || (k % 6 == 5))
			{
			}
			else
			{
				if (BtnColorStore[(k - 5)].Equals(Colors[1]))
				{
					nCount[l]++;
				}
			}
			// left
			if ((k - 1 < 0) || (k % 6 == 0))
			{
			}
			else
			{
				if (BtnColorStore[(k - 1)].Equals(Colors[1]))
				{
					nCount[l]++;
				}
			}
			// right
			if ((k + 1 > 47) || (k % 6 == 5))
			{
			}
			else
			{
				if (BtnColorStore[(k + 1)].Equals(Colors[1]))
				{
					nCount[l]++;
				}
			}
			// bottom left
			if ((k + 5 > 47) || (k % 6 == 0))
			{
			}
			else
			{
				if (BtnColorStore[(k + 5)].Equals(Colors[1]))
				{
					nCount[l]++;
				}
			}
			// bottom
			if (k + 6 > 47)
			{
			}
			else
			{
				if (BtnColorStore[(k + 6)].Equals(Colors[1]))
				{
					nCount[l]++;
				}
			}
			// bottom right
			if ((k + 7 > 47) || (k % 6 == 5))
			{
			}
			else
			{
				if (BtnColorStore[(k + 7)].Equals(Colors[1]))
				{
					nCount[l]++;
				}
			}

			// read nCount and decide life state
			if (BtnColor[k].material.color == Colors[1])
			{   //if square is white
				if (nCount[k] < 2 || nCount[k] > 3)
				{
					BtnColor[l].material.color = Colors[0];
					BtnColor1[l] = 0;
					BtnColor2[l] = 0;
				}
			}
			else
			{                                           //if square is black
				if (nCount[k] == 3)
				{
					BtnColor[l].material.color = Colors[1];
					BtnColor1[l] = 1;
					BtnColor2[l] = 1;
				}
			}
		}
	}

	/////////////////////////////////////////////////// Button presses ///////////////////////////////////////////////////////

	// square is pressed
	void handleSquare (int num) {
		if (BtnColor [num].material.color == Colors [0]) {
			BtnColor [num].material.color = Colors [1];
			BtnColor1 [num] = 1;
			BtnColor2 [num] = 1;
		} else {
			BtnColor [num].material.color = Colors [0];
			BtnColor1 [num] = 0;
			BtnColor2 [num] = 0;
		}
	}

	// submit is pressed
	private IEnumerator handleSubmit () {
		isSubmitting = true;
		updateDebug ("Submitted");
		yield return new WaitForSeconds (TimeTiny);

		// store the submitted color values
		for (int i = 0; i < 48; i++) {
			ColorsSubmitted [i] = BtnColor [i].material.color;
		}

		// run a reset
		updateReset();
		yield return new WaitForSeconds (TimeTiny * 20);

		// process the generation
		simulateGeneration();
		updateSquares ();

		yield return new WaitForSeconds (TimeTiny);

		// test last generation vs ColorsSubmitted
		string[] errorNumbers = Enumerable.Range(0, 48).Where(i => BtnColor[i].material.color != ColorsSubmitted[i]).Select(i => (char) (i % 6 + 65) + "" + (Mathf.FloorToInt(i / 6) + 1)).ToArray();
		if (errorNumbers.Length > 0)
		{
			Log("Found error{0} at square{0} {1}. Strike", errorNumbers.Length > 1 ? "s" : "", string.Join(", ", errorNumbers));
			Module.HandleStrike();
			yield return new WaitForSeconds(TimeSneak);
			isSubmitting = false;
			updateReset();
		}

		//solve!
		if (isSubmitting == true) {
			Log("No errors found! Module passed");
			Module.HandlePass ();
			isSolved = true;
		}

		yield return false;
	}

	#pragma warning disable 414
    private string TwitchHelpMessage = "Clear the grid: !{0} clear. Toggle a cell by giving its coordinate: !{0} a1 b2. Submit your answer: !{0} submit. Reset back to the intial state: !{0} reset. All commands are chainable.";
	#pragma warning restore 414

    List<KMSelectable> ProcessTwitchCommand(string inputCommand)
    {
		string[] split = inputCommand.ToLowerInvariant().Split(new[] { ' ', ';', ',' }, StringSplitOptions.RemoveEmptyEntries);

		Dictionary<string, KMSelectable> buttonNames = new Dictionary<string, KMSelectable>()
		{
			{ "clear", Clear },
			{ "c", Clear },
			{ "reset", Reset },
			{ "r", Reset },
			{ "submit", Submit },
			{ "s", Submit },
		};

		List<KMSelectable> buttons = new List<KMSelectable>();
		foreach (string item in split)
		{
			KMSelectable button;
			if (item.Length == 2)
			{
				int x = item[0] - 'a';
				int y = item[1] - '1';
				if (x < 0 || y < 0 || x > 5 || y > 7) return null;

				buttons.Add(Btn[(y * 6) + x]);
			}
			else if (buttonNames.TryGetValue(item, out button))
			{
				buttons.Add(button);
			}
			else
			{
				return null;
			}
		}

		return buttons.Count > 0 ? buttons : null;
    }
}