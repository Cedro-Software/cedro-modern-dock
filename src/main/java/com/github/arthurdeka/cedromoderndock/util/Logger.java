package com.github.arthurdeka.cedromoderndock.util;

import java.io.BufferedWriter;
import java.io.File;
import java.io.FileWriter;
import java.io.IOException;
import java.io.PrintWriter;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.time.LocalDateTime;
import java.time.format.DateTimeFormatter;

/**
 * Final utility class for application logging.
 * Provides static methods for informational and error logs.
 */
public final class Logger {

    private static final String LOG_FILE_NAME = "log.txt";
    private static final Path LOG_FILE_PATH = getLogFilePath();
    private static final DateTimeFormatter dtf = DateTimeFormatter.ofPattern("yyyy-MM-dd HH:mm:ss");

    // Private constructor to prevent this utility class from being instantiated.
    private Logger() {
    }

    /**
     * Determines the absolute path for the log file in the user's
     * AppData/Roaming directory.
     * @return The absolute Path to the log file.
     */
    private static Path getLogFilePath() {
        // Get the AppData\Roaming folder path (standard for user-specific data, same for config.json file)
        String appDataPath = System.getenv("APPDATA");
        if (appDataPath == null || appDataPath.isEmpty()) {
            // Fallback to user home directory if APPDATA is not available
            appDataPath = System.getProperty("user.home");
        }

        // Create a dedicated directory for our application to keep things clean
        Path configDir = Paths.get(appDataPath, "CedroModernDock");

        // Ensure the directory exists
        File dir = configDir.toFile();
        if (!dir.exists()) {
            dir.mkdirs();
        }

        // Return the full path to our log file
        return configDir.resolve(LOG_FILE_NAME);
    }

    /**
     * Logs an informational message to the console.
     * The format includes the [INFO] log level.
     */
    public static void info(String message) {
        System.out.println("[INFO] - " + message);
    }

    /**
     * Logs an error message to a log file (log.txt).
     * Messages are appended to the end of the file.
     */
    public static void error(String message) {
        String timestamp = dtf.format(LocalDateTime.now());
        String logMessage = String.format("[%s] [ERROR] %s", timestamp, message);

        // Prints a notification to the console that an error has been logged.
        System.err.println(logMessage + " | Check " + LOG_FILE_PATH + " for more details.");

        try (FileWriter fw = new FileWriter(LOG_FILE_PATH.toFile(), true);
             BufferedWriter bw = new BufferedWriter(fw);
             PrintWriter out = new PrintWriter(bw)) {

            out.println(logMessage);
            out.println("----------------------------------------------------");

        } catch (IOException e) {
            System.err.println("CRITICAL ERROR: Could not write to the log file.");
            e.printStackTrace();
        }
    }
}
