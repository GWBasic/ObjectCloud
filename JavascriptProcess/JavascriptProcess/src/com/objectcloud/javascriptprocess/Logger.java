package com.objectcloud.javascriptprocess;

import java.io.BufferedWriter;
import java.io.FileWriter;
import java.io.IOException;
import java.util.Date;



// Assists in logging timing issues
public class Logger {
	
	static {
		Date now = new Date();
		filename = (new Long(now.getTime())).toString() + ".log";
	}

	private static String filename;
	
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

	}
}
