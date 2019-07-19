using System;
using System.Collections.Generic;

public class Stats {
	private DateTime startTime;
	private int indent;
	private List<string> lines;
	// private List<Section> sections;

	private string IndentString() {
		string str = "";
		for (int i = 0; i < indent; i++) {
			str += " ";
		}
		return str;
	}

	private void AddLine(string line) {
		string time = DateTime.Now.ToString();
		string outputLine = IndentString()+line+" - "+time;
		lines.Add(outputLine);
	}

	public void SaveToDisk() {
		var text = String.Join("\n", lines.ToArray());
		System.IO.File.WriteAllText(@"stats.txt", text);
	}

	public void Pop() {
		indent -= 1;
	}

	public void Push(string section, string[] options = null) {

		// TODO: we need a date delta so we do indeed need a stack
		indent += 1;
		string time = DateTime.Now.ToString();
		string line = "";

		line += IndentString();
		line += section;
		if (options != null) {
			line += String.Join("	", options);
			line += " ";
		}
		line += " => "+time;
		lines.Add(line);
	}

	public Stats() {
		startTime = DateTime.Now;
		lines = new List<string>();
		AddLine("app started");
	}
}