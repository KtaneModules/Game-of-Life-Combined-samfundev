using System;
using UnityEngine;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;

public class GameOfLifeCruel : MonoBehaviour {

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
	string[] ColorNames = { "black", "white", "red", "orange", "yellow", "green", "blue", "purple", "brown" };
	private bool[] Rules = new bool[9]; 

	private int BlackAmount = 32;		// amount of black squares generated, at average, in initial setup
	private int WhiteAmount = 12;		// amount of white squares generated, at average, in initial setup
	private float TimeFlash = 0.5f;		// time between flashes
	//private float TimeSuspend = 0.8f;	// time between generation when submitting
	private float TimeSneak = 0.4f;		// time the correct solution is displayed at a strike
	private float TimeTiny = 0.01f;		// time to allow computations in correct order. set to as low as possible

	private int iiLast;
	private int iiBatteries;
	private int iiLit;
	private int iiUnlit;
	private int iiPortTypes;
	private int iiStrikes;
	private int iiSolved;
	private float iiTimeRemaining;
	private float iiTimeOriginal;
	private bool Bob;

	private bool isActive = false;
	private bool isSolved = false;
	private bool isSubmitting = false;

	private static int moduleIdCounter = 1;
	private int moduleId = 0;

	private Dictionary<int, int[]> secondaryColorComponents = new Dictionary<int, int[]>()
	{
		{3, new[] { 2, 4 }},
		{5, new[] { 4, 6 }},
		{7, new[] { 2, 6 }},
	};

	/////////////////////////////////////////////////// Initial Setup ///////////////////////////////////////////////////////

	// Loading screen
	void Start () {

		moduleId = moduleIdCounter++;
		Module.OnActivate += Activate;
	}

	// Lights off
	void Awake () {

		//assign button presses
		Clear.OnInteract += delegate ()
		{
			Audio.PlayGameSoundAtTransform (KMSoundOverride.SoundEffect.ButtonPress, Clear.transform);
			Clear.AddInteractionPunch ();
			Bob = false;

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
				updateReset();
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
		InitSetup ();

		StartCoroutine(updateTick ());

		updateDebug("Initial state");
		
		updateBool();

		for (int modules = 0; modules <= 1; modules++)
		{
			if (Info.IsIndicatorPresent(Indicator.CLR) && modules == 1) continue;
			Rules[5] = modules == 1;

			for (int time = 0; time <= 1; time++)
			{
				if (Info.IsIndicatorPresent(Indicator.CAR) && time == 1) continue;
				Rules[3] = time == 1;

				for (int strike = 0; strike <= 1; strike++)
				{
					if (iiBatteries == 0 && strike == 1) continue;
					Rules[2] = strike == 1;

					updateReset();
					calculateColors();
					if (isSolved) return; // Check if something failed to calculate and abort.
					string rules = string.Join(", ", new[] { iiBatteries == 0 ? "any number of strikes" : Rules[2] ? "at least one strike" : "no strikes", Info.IsIndicatorPresent(Indicator.CAR) ? "any time" : Rules[3] ? "less than half the time remaining" : "more than half the time remaining", Info.IsIndicatorPresent(Indicator.CLR) ? "any number of solved modules" : Rules[5] ? "even number of solved modules" : "odd number of solved modules" });
					Log("Answer for {0}:", rules);
					updateDebug("Colored square states");

					updateSquares();
					simulateGeneration();
					updateDebug("Solution");
				}
			}
		}

		updateBool();
		updateReset();

		isActive = true;
	}

	// Initial setup
	List<int[]> loggingNumbers = new List<int[]>();
	void InitSetup () {
		iiTimeOriginal = Info.GetTime ();
		Bob = true;

		string colorLegend = "Cell color reference:\n◼ = Black\n◻ = White";
		for (int i = 0; i < 48; i++)
		{
			// radomizing starting squares
			int x = Random.Range (0, 48);
			if (x < BlackAmount) {      // black, black
				BtnColor1[i] = BtnColor1init[i] = 0;
				BtnColor2[i] = BtnColor2init[i] = 0;
			} else if (x < (BlackAmount + WhiteAmount)) {       // white, white
				BtnColor1[i] = BtnColor1init [i] = 1;
				BtnColor2[i] = BtnColor2init[i] = 1;
			} else {                                    // others randomized
				BtnColor1init[i] = Random.Range (0, 9);
				if (BtnColor1init [i] == 1)
					BtnColor1init [i] = 0;
				BtnColor2init[i] = Random.Range (0, 9);
				if (BtnColor2init [i] == 1)
					BtnColor2init [i] = 0;
				BtnColor1 [i] = BtnColor1init [i];
				BtnColor2 [i] = BtnColor2init [i];
				
				int Color1 = BtnColor1init[i];
				int Color2 = BtnColor2init[i];

				if (Color1 == 0 && Color2 == 0) continue;

				int[] colors = new int[2] { Color1, Color2 };
				if (Color2 > Color1)
				{
					colors = new int[2] { Color2, Color1 };
				}

				if (loggingNumbers.Any(colors.SequenceEqual)) continue;

				loggingNumbers.Add(colors);
				string description = null;
				if (Color1 == Color2) description = "Steady {0}";
				else if (Color1 == 0) description = "Flashing {1}";
				else if (Color2 == 0) description = "Flashing {0}";
				else description = "Flashing {0}/{1}";

				colorLegend += string.Format("\n{0} = {1}", loggingNumbers.Count, string.Format(description, ColorNames[Color1], ColorNames[Color2]));
			}
		}

		Log(colorLegend);
	}

	// Log function
	void Log(string message, params object[] args)
	{
		Debug.LogFormat("[Game of Life Cruel #{0}] {1}", moduleId, string.Format(message, args));
	}

	/////////////////////////////////////////////////// Updates ///////////////////////////////////////////////////////

	// update the booleans for rules
	void updateBool () {

		iiLast = Info.GetSerialNumberNumbers ().Last ();
		iiBatteries = Info.GetBatteryCount ();
		iiLit = Info.GetOnIndicators ().Count ();
		iiUnlit = Info.GetOffIndicators ().Count ();
		iiPortTypes = Info.GetPorts ().Distinct ().Count ();
		iiStrikes = Info.GetStrikes ();
		iiSolved = Info.GetSolvedModuleNames ().Count ();
		iiTimeRemaining = Info.GetTime ();

		Rules[2] = iiStrikes > 0 && iiBatteries != 0;																			//red		needs update
		Rules[3] = (iiTimeRemaining < (iiTimeOriginal / 2)) && !Info.IsIndicatorPresent(Indicator.CAR);							//orange	needs update
		Rules[4] = (iiLit > iiUnlit) && !Info.IsPortPresent(Port.RJ45);															//yellow
		Rules[5] = (iiSolved % 2 == 0) && !Info.IsIndicatorPresent(Indicator.CLR);												//green		needs update
		Rules[6] = Info.GetSerialNumberLetters().Any("SEAKY".Contains) && !Info.IsIndicatorPresent(Indicator.SND);				//blue
		Rules[7] = (iiLit < iiUnlit) && (iiBatteries < 4);																		//purple
		Rules[8] = (iiPortTypes > 2) && ((iiLit + iiUnlit) > 0);                                                                //brown
	}

	// automatic update of squares
	private IEnumerator updateTick()
	{
		while (true)
		{
			if (isActive && !isSubmitting) // check if module is active but not submitting. if yes, then update
			{                       
				updateSquares();
			}

			yield return new WaitForSeconds(TimeFlash);
		}
	}

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
		updateSquares ();
		Bob = true;
	}

	// display current state in debug log
	private void updateDebug (string title = "State") {
		string logString = title + ":\n";
		for (int d = 0; d < 48; d++) {
			if (BtnColor1 [d] == 0 && BtnColor2 [d] == 0) {
				logString += "◼";
			} else if (BtnColor1 [d] == 1 && BtnColor2 [d] == 1) {
				logString += "◻";
			} else {
				int Color1 = BtnColor1[d];
				int Color2 = BtnColor2[d];
				int[] colors = new int[2] { Color1, Color2 };
				if (Color2 > Color1)
				{
					colors = new int[2] { Color2, Color1 };
				}
				
				logString += loggingNumbers.FindIndex(colors.SequenceEqual) + 1;
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

	void calculateColors()
	{
		for (int i = 0; i < 48; i++)
		{
			int Color1 = BtnColor1[i];
			int Color2 = BtnColor2[i];

			if ((Color1 == 0 || Color1 == 1) && Color1 == Color2) continue;
			
			Func<int, int, bool?>[] colorRules =
			{
				(c1, c2) => (Color1 == Color2) ? (bool?) Rules[Color1] : null,
				(c1, c2) => (c1 >= 2 && c2 == 0) ? (bool?) !Rules[c1] : null,
				(c1, c2) => {
					if (c1 % 2 == 0 && c1 != 8 && c2 % 2 == 0 && c2 != 8) // Rule 3
					{
						int mixed = -1;
						if (c1 == 2 && c2 == 4) // Red & Yellow
						{
							mixed = 3;
						}
						else if (c1 == 6 && c2 == 4) // Blue & Yellow
						{
							mixed = 5;
						}
						else if (c1 == 2 && c2 == 6) // Red & Blue
						{
							mixed = 7;
						}
						else return null;

						return Rules[mixed];
					}
					return null;
				},
				(c1, c2) => (c1 % 2 == 0 && c1 != 8 && c2 % 2 == 1) ? (bool?) Rules[secondaryColorComponents[c2].Contains(c1) ? c1 : c2] : null,
				(c1, c2) => (c1 % 2 == 1 && c2 % 2 == 1) ? (bool?) Rules[secondaryColorComponents[c1].Intersect(secondaryColorComponents[c2]).First()] : null,
				(c1, c2) => (c1 == 8 && c2 >= 2) ? (bool?) Rules[iiLast % 2 == 0 ? 8 : c2] : null
			};
			
			bool? cellColor = null;
			foreach (var rule in colorRules)
			{
				bool? result = rule(Color1, Color2);
				if (result != null) cellColor = result;
				else cellColor = rule(Color2, Color1);

				if (cellColor != null) break;
			}
			
			if (cellColor == null)
			{
				Log("Something went wrong when figuring out the color of a cell. Debug Info: C1: {0}, C2: {1}", Color1, Color2);
				Module.HandlePass();
				isSolved = true;
				isSubmitting = false;
				break;
			}

			BtnColor1[i] = BtnColor2[i] = Convert.ToInt32(cellColor);
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
		Bob = false;
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

		// bob helps out? 
		if ((Info.GetBatteryCount () == 6) && (Info.GetBatteryHolderCount () == 3) && (Bob == true) && (Info.IsIndicatorOff(Indicator.BOB))) {
			Module.HandlePass ();
			Log("Bob has assisted you. Time to party!");
			for (int i = 0; i < 48; i++) {
				BtnColor1[i] = Random.Range (2, 9);
				BtnColor2[i] = Random.Range (2, 9);
			}
			isSolved = true;
			isSubmitting = false;
		} else {

			// store the submitted color values
			for (int i = 0; i < 48; i++) {
				ColorsSubmitted [i] = BtnColor [i].material.color;
			}

			// run a reset
			updateReset();
			yield return new WaitForSeconds (TimeTiny * 20);

			// transform colored squares into black or white, according to rules (update rules first)
			updateBool ();

			calculateColors();
			if (isSolved) yield break;
			string[] parts = new[] { iiBatteries == 0 ? "any number of strikes" : Rules[2] ? "at least one strike" : "no strikes", Info.IsIndicatorPresent(Indicator.CAR) ? "any time" : Rules[3] ? "less than half the time remaining" : "more than half the time remaining", Info.IsIndicatorPresent(Indicator.CLR) ? "any number of solved modules" : Rules[5] ? "even number of solved modules" : "odd number of solved modules" };
			string[] parentheses = new[] { iiStrikes.ToString(), Info.GetFormattedTime(), iiSolved.ToString() };
			for (int i = 0; i < 3; i++)
			{
				parts[i] += " (" + parentheses[i] + ")";
			}

			string rules = string.Join(", ", parts);
			Log("Submitted at {0}:", rules);

			// update squares to show state of colors fixed, then wait for sneak
			updateSquares ();
			updateDebug ("Colored square states");
			yield return new WaitForSeconds (TimeSneak);

			simulateGeneration();

			// update squares, wait, then next generation
			updateSquares ();
			updateDebug ("Solution");

			// test last generation vs ColorsSubmitted
			string[] errorNumbers = Enumerable.Range(0, 48).Where(i => BtnColor[i].material.color != ColorsSubmitted[i]).Select(i => (char) (i % 6 + 65) + "" + (Mathf.FloorToInt(i / 6) + 1)).ToArray();
			if (errorNumbers.Length > 0)
			{
				Log("Found error{0} at square{0} {1}. Strike", errorNumbers.Length > 1 ? "s" : "", string.Join(", ", errorNumbers));
				Module.HandleStrike ();
				yield return new WaitForSeconds (TimeSneak);
				isSubmitting = false;
				updateReset ();
			}

			//solve!
			if (isSubmitting == true) {
				Log("No errors found! Module passed");
				Module.HandlePass ();
				isSolved = true;
			}

			yield return false;
		}
	}

	#pragma warning disable 414
	private string TwitchHelpMessage = "Clear the grid: !{0} clear. Toggle a cell by giving its coordinate: !{0} a1 b2. Submit your answer: !{0} submit. Reset back to the intial state: !{0} reset. All commands are chainable.";
	#pragma warning restore 414

	KMSelectable[] ProcessTwitchCommand(string inputCommand)
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

		return buttons.Count > 0 ? buttons.ToArray() : null;
	}
}