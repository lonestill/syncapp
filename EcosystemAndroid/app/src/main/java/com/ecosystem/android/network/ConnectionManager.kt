package com.ecosystem.android.network

import android.util.Log
import com.ecosystem.android.model.Device
import com.ecosystem.android.model.DeviceStatus
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import java.util.concurrent.locks.ReentrantLock
import kotlin.concurrent.withLock
import java.io.BufferedReader
import java.io.InputStreamReader
import java.io.OutputStreamWriter
import java.net.InetSocketAddress
import java.net.Socket

class ConnectionManager(
    private val onConnected: () -> Unit,
    private val onDisconnected: () -> Unit,
    private val onMessageReceived: (String) -> Unit,
    private val onConnectionError: (String) -> Unit
) {
    private val TAG = "ConnectionManager"
    private val MAX_RECONNECT_ATTEMPTS = 2
    private val RECONNECT_DELAY = 2000L
    private val KEEPALIVE_INTERVAL = 5000L

    private var socket: Socket? = null
    private var outputWriter: OutputStreamWriter? = null
    private var inputReader: BufferedReader? = null
    private var isConnected = false
    private var reconnectAttempts = 0
    private var keepaliveJob: kotlinx.coroutines.Job? = null
    private val writeLock = ReentrantLock()

    fun connect(device: Device) {
        CoroutineScope(Dispatchers.IO).launch {
            try {
                socket = Socket()
                socket?.connect(InetSocketAddress(device.ipAddress, device.port), 5000)
                socket?.soTimeout = 10000
                outputWriter = OutputStreamWriter(socket?.getOutputStream(), Charsets.UTF_8)
                inputReader = BufferedReader(InputStreamReader(socket?.getInputStream(), Charsets.UTF_8))

                // Send connection message
                val deviceName = android.os.Build.MODEL
                sendMessage("CONNECT:$deviceName:Android")

                // Wait for acceptance
                val response = inputReader?.readLine()
                if (response?.startsWith("ACCEPT:") == true) {
                    isConnected = true
                    reconnectAttempts = 0
                    device.status = DeviceStatus.CONNECTED
                    
                    onConnected()
                    startKeepalive()
                    startMessageReceiver()
                } else {
                    throw Exception("Connection rejected")
                }
            } catch (e: Exception) {
                Log.e(TAG, "Connection failed", e)
                onConnectionError("Connection failed: ${e.message}")
                device.status = DeviceStatus.ERROR
                safeClose()
                handleReconnection(device)
            }
        }
    }

    private fun startKeepalive() {
        keepaliveJob = CoroutineScope(Dispatchers.IO).launch {
            while (isConnected) {
                try {
                    sendMessage("PING")
                    delay(KEEPALIVE_INTERVAL)
                } catch (e: Exception) {
                    Log.e(TAG, "Keepalive error", e)
                    break
                }
            }
        }
    }

    private fun startMessageReceiver() {
        CoroutineScope(Dispatchers.IO).launch {
            try {
                while (isConnected) {
                    val message = inputReader?.readLine()
                    if (message == null) {
                        handleDisconnection()
                        break
                    }

                    when {
                        message == "PONG" -> {
                            // Keepalive response
                        }
                        message == "PING" -> {
                            sendMessage("PONG")
                        }
                        message.startsWith("DISCONNECT") -> {
                            disconnect()
                        }
                        else -> {
                            onMessageReceived(message)
                        }
                    }
                }
            } catch (e: Exception) {
                Log.e(TAG, "Message receiver error", e)
                handleDisconnection()
            }
        }
    }

    fun sendMessage(message: String) {
        try {
            writeLock.withLock {
                outputWriter?.write("$message\n")
                outputWriter?.flush()
            }
        } catch (e: Exception) {
            Log.e(TAG, "Send message error", e)
            onConnectionError("Failed to send message: ${e.message}")
        }
    }

    fun disconnect() {
        try {
            isConnected = false
            keepaliveJob?.cancel()
            sendMessage("DISCONNECT")
            
            outputWriter?.close()
            inputReader?.close()
            socket?.close()
            
            onDisconnected()
        } catch (e: Exception) {
            Log.e(TAG, "Disconnect error", e)
        }
    }

    private fun safeClose() {
        try { outputWriter?.close() } catch (_: Exception) {}
        try { inputReader?.close() } catch (_: Exception) {}
        try { socket?.close() } catch (_: Exception) {}
    }

    private fun handleDisconnection() {
        if (isConnected) {
            isConnected = false
            keepaliveJob?.cancel()
            onDisconnected()
        }
    }

    private fun handleReconnection(device: Device) {
        if (reconnectAttempts < MAX_RECONNECT_ATTEMPTS) {
            reconnectAttempts++
            CoroutineScope(Dispatchers.IO).launch {
                delay(RECONNECT_DELAY)
                connect(device)
            }
        }
    }

    fun isConnected(): Boolean = isConnected
}