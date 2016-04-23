﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace UnityTwine.Editor.StoryFormats.Harlowe
{
	public delegate int HarloweCodeGenMacro(HarloweTranscoder transcoder, LexerToken[] tokens, int macroTokenIndex, MacroUsage usage);

	public enum MacroUsage
	{
		Inline,
		Line,
		LineAndHook
	}

	public static class BuiltInCodeGenMacros
	{
		// ......................
		public static HarloweCodeGenMacro Assignment = (transcoder, tokens, tokenIndex, usage) =>
		{
			LexerToken assignToken = tokens[tokenIndex];

			if (usage == MacroUsage.Inline)
				throw new TwineTranscodeException(string.Format("'{0}' macro cannot be used inside another macro", assignToken.name));

            //if (assignToken.name.ToLower() == "move")
             //   throw new TwineTranscodeException(string.Format("The 'move' macro is not currently supported. Use 'set' or 'put'.", assignToken.name));

			int start = 1;
			int end = start;
			for (; end < assignToken.tokens.Length; end++)
			{
				LexerToken token = assignToken.tokens[end];
				if (token.type == "comma")
				{
					transcoder.GenerateAssignment(assignToken.name.ToLower(), assignToken.tokens, start, end - 1);
					transcoder.Code.Buffer.Append("; ");
					start = end + 1;
				}
			}
			if (start < end)
			{
				transcoder.GenerateAssignment(assignToken.name.ToLower(), assignToken.tokens, start, end - 1);
				transcoder.Code.Buffer.Append(";");
			}

			transcoder.Code.Buffer.AppendLine();
			
			return tokenIndex;
		};

		// ......................
		public static HarloweCodeGenMacro Conditional = (transcoder, tokens, tokenIndex, usage) =>
		{
			LexerToken token = tokens[tokenIndex];

			if (usage == MacroUsage.Line)
				throw new TwineTranscodeException("'" + token.name + "' must be followed by a Harlowe-hook.");
			if (usage == MacroUsage.Inline)
				throw new TwineTranscodeException("'" + token.name + "' cannot be used inline.");

			transcoder.Code.Buffer.Append(
				token.name == "elseif" ? "else if" :
				token.name == "else" ? "else" :
				"if");

			if (token.name != "else")
			{
				transcoder.Code.Buffer.Append("(");
				if (token.name == "unless")
					transcoder.Code.Buffer.Append("!(");
				transcoder.GenerateExpression(token.tokens, start: 1);
				if (token.name == "unless")
					transcoder.Code.Buffer.Append(")");
				transcoder.Code.Buffer.AppendLine(") {");
			}
			else
				transcoder.Code.Buffer.AppendLine(" {");

			transcoder.Code.Indentation++;

			// Advance to hook
			tokenIndex++;

			LexerToken hookToken = tokens[tokenIndex];
			transcoder.GenerateBody(hookToken.tokens);

			transcoder.Code.Indentation--;
			transcoder.Code.Indent();
			transcoder.Code.Buffer.AppendLine("}");

			// Skip any trailing line breaks and whitespace before the next else or elseif
			if (token.name != "else")
			{
				int i = tokenIndex + 1;
				while (i < tokens.Length)
				{
					if (tokens[i].type != "br" && tokens[i].type != "whitespace")
					{
						// Jump to the position before this macro. Otherwise we'll just continue as normal
						if (tokens[i].type == "macro" && (tokens[i].name == "elseif" || tokens[i].name == "else"))
							tokenIndex = i - 1;

						break;
					}
					else
						i++;
				}
			}

			return tokenIndex;
		};

		// ......................
		public static HarloweCodeGenMacro Link = (transcoder, tokens, tokenIndex, usage) =>
		{
			LexerToken linkToken = tokens[tokenIndex];

			// Text
			transcoder.Code.Buffer.AppendFormat("yield return link(text: ");
			int start = 1;
			int end = start;
			for (; end < linkToken.tokens.Length; end++)
				if (linkToken.tokens[end].type == "comma")
					break;
			transcoder.GenerateExpression(linkToken.tokens, start: start, end: end - 1);

			// Passage
			transcoder.Code.Buffer.Append(", passageName: ");
			start = ++end;
			for (; end < linkToken.tokens.Length; end++)
				if (linkToken.tokens[end].type == "comma")
					break;
			if (start < end)
				transcoder.GenerateExpression(linkToken.tokens, start: start, end: end - 1);
			else
				transcoder.Code.Buffer.Append("null");

			// Action
			transcoder.Code.Buffer.Append(", action: ");
			if (linkToken.name != "linkgoto")
			{
				if (usage == MacroUsage.LineAndHook)
				{
					tokenIndex++; // advance
					LexerToken hookToken = tokens[tokenIndex];
					transcoder.Code.Buffer.Append(transcoder.GenerateFragment(hookToken.tokens));
				}
				else
					throw new TwineTranscodeException(string.Format("'{0}' macro must be followed by a Harlowe-hook", linkToken.name));
			}
			else
				transcoder.Code.Buffer.Append("null");

			// Parameters
			transcoder.Code.Buffer
				.Append(", new parameters(){ ")
				.AppendFormat("{\"macro\", \"{0}\"},", linkToken.name)
				.Append("}");

			// Done
			transcoder.Code.Buffer.AppendLine(");");

			return tokenIndex;
		};

		// ......................
		public static HarloweCodeGenMacro Enchant = (transcoder, tokens, tokenIndex, usage) =>
		{
			LexerToken linkToken = tokens[tokenIndex];

			// Text
			transcoder.Code.Buffer.AppendFormat("yield return enchant(text: null, ");
			int start = 1;
			int end = start;
			for (; end < linkToken.tokens.Length; end++)
				if (linkToken.tokens[end].type == "comma")
					break;
			transcoder.GenerateExpression(linkToken.tokens, start: start, end: end - 1);

			// Passage
			transcoder.Code.Buffer.Append(", passageName: ");
			start = ++end;
			for (; end < linkToken.tokens.Length; end++)
				if (linkToken.tokens[end].type == "comma")
					break;
			if (start < end)
				transcoder.GenerateExpression(linkToken.tokens, start: start, end: end - 1);
			else
				transcoder.Code.Buffer.Append("null");

			// Action
			transcoder.Code.Buffer.Append(", action: ");
			if (linkToken.name != "linkgoto")
			{
				if (usage == MacroUsage.LineAndHook)
				{
					tokenIndex++; // advance
					LexerToken hookToken = tokens[tokenIndex];
					transcoder.Code.Buffer.Append(transcoder.GenerateFragment(hookToken.tokens));
				}
				else
					throw new TwineTranscodeException(string.Format("'{0}' macro must be followed by a Harlowe-hook", linkToken.name));
			}
			else
				transcoder.Code.Buffer.Append("null");

			// Parameters
			transcoder.Code.Buffer
				.Append(", new parameters(){ ")
				.AppendFormat("{\"macro\", \"{0}\"},", linkToken.name)
				.Append("}");

			// Done
			transcoder.Code.Buffer.AppendLine(");");

			return tokenIndex;
		};

		// ......................
		public static HarloweCodeGenMacro GoTo = (transcoder, tokens, tokenIndex, usage) =>
		{
			if (usage == MacroUsage.Inline)
				throw new TwineTranscodeException("GoTo macro cannot be used inside another macro");

			transcoder.Code.Buffer.Append("yield return abort(goToPassage: ");
			transcoder.GenerateExpression(tokens[tokenIndex].tokens, 1);
			transcoder.Code.Buffer.AppendLine(");");

			return tokenIndex;
		};

        // ......................
        public static HarloweCodeGenMacro Display = (transcoder, tokens, tokenIndex, usage) =>
        {
            if (usage == MacroUsage.Inline)
                throw new TwineTranscodeException("Display macro cannot be used inside another macro");

            transcoder.Code.Buffer.Append("yield return passage(");
            transcoder.GenerateExpression(tokens[tokenIndex].tokens, 1);
            transcoder.Code.Buffer.AppendLine(");");

            return tokenIndex;
        };

		// ......................
		public static HarloweCodeGenMacro Style = (transcoder, tokens, tokenIndex, usage) =>
		{
			LexerToken macroToken = tokens[tokenIndex];

			if (usage == MacroUsage.Inline)
			{
				transcoder.Code.Buffer.AppendFormat("style(\"{0}\", ", macroToken.name);
				transcoder.GenerateExpression(macroToken.tokens, start: 1);
				transcoder.Code.Buffer.Append(")");
			}
			else if (usage == MacroUsage.LineAndHook)
			{
				transcoder.Code.Buffer.AppendFormat("using (Style.Apply(\"{0}\", ", macroToken.name);
				transcoder.GenerateExpression(macroToken.tokens, start: 1);
				transcoder.Code.Buffer.AppendLine(")) {");

				// Advance to hook
				tokenIndex++;
				LexerToken hookToken = tokens[tokenIndex];

				transcoder.Code.Indentation++;
				transcoder.GenerateBody(hookToken.tokens, false);
				transcoder.Code.Indentation--;
				transcoder.Code.Indent();
				transcoder.Code.Buffer.AppendLine("}");
			}
			else
				throw new TwineTranscodeException(string.Format("The '{0}' macro must either be attached to a Harlowe-hook or assigned to a variable.", macroToken.name));		

			return tokenIndex;
		};

		// ......................
		public static HarloweCodeGenMacro Print = (transcoder, tokens, tokenIndex, usage) =>
		{
			if (usage != MacroUsage.Inline)
			{
				transcoder.Code.Buffer.Append("yield return text(");
				transcoder.GenerateExpression(tokens[tokenIndex].tokens, 1);
				transcoder.Code.Buffer.AppendLine(");");
			}
			else
				transcoder.Code.Buffer.Append("null");

			return tokenIndex;
		};

		// ......................

		public static HarloweCodeGenMacro RuntimeMacro = (transcoder, tokens, tokenIndex, usage) =>
		{
			LexerToken macroToken = tokens[tokenIndex];
			MacroDef macroDef;
			if (!transcoder.Importer.Macros.TryGetValue(macroToken.name, out macroDef))
				throw new TwineImportException(string.Format("Macro '{0}' is not defined as a UnityTwine runtime macro.", macroToken.name));

			transcoder.Code.Buffer.AppendFormat("{0}.{1}(", macroDef.Lib.Name, HarloweTranscoder.EscapeReservedWord(macroDef.Name));
			transcoder.GenerateExpression(macroToken.tokens, 1);
			transcoder.Code.Buffer.Append(")");

			if (usage != MacroUsage.Inline)
				transcoder.Code.Buffer.AppendLine(";");

			return tokenIndex;
		};
	}
}