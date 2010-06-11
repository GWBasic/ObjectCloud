package com.objectcloud.javascriptprocess;

import java.io.BufferedWriter;
import java.io.FileWriter;
import java.io.IOException;

// Assists in logging timing issues
public class Logger {
	
	/*public static void setFilename(String filename) {
		Logger.filename = filename;
	}

	public static String getFilename() {
		return filename;
	}

	private static String filename = null;
	
	// Logs to the file, if set
	public static synchronized void log(String toLog) {
		
		if (null == filename)
			return;
		
		try {
			BufferedWriter bw = new BufferedWriter(new FileWriter(filename, true));

			try {
				bw.write(toLog + "\n");
				bw.newLine();
				bw.flush();
			} catch (IOException ioe) {
			} finally {
				try {
					bw.close();
				} catch (IOException ioe2) {
				}
			}
		} catch (IOException e) {
		}

	}*/
}
