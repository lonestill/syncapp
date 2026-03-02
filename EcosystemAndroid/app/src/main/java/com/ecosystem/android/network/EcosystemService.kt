package com.ecosystem.android.network

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.os.Build
import android.os.IBinder
import android.util.Log
import androidx.core.app.NotificationCompat
import com.ecosystem.android.R
import com.ecosystem.android.model.Device
import com.ecosystem.android.ui.MainActivity

class EcosystemService : Service() {
    private val TAG = "EcosystemService"
    private val CHANNEL_ID = "ecosystem_service_channel"
    private val NOTIFICATION_ID = 1

    private var networkDiscovery: NetworkDiscovery? = null
    private var connectionManager: ConnectionManager? = null
    private var connectedDevice: Device? = null
    private var isConnecting = false

    override fun onCreate() {
        super.onCreate()
        try {
            createNotificationChannel()
            if (Build.VERSION.SDK_INT >= 34) {
                startForeground(
                    NOTIFICATION_ID,
                    createNotification(),
                    android.content.pm.ServiceInfo.FOREGROUND_SERVICE_TYPE_SPECIAL_USE
                )
            } else {
                startForeground(NOTIFICATION_ID, createNotification())
            }
        } catch (e: Exception) {
            Log.e(TAG, "Failed to start foreground", e)
            stopSelf()
        }
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        when (intent?.action) {
            ACTION_START -> {
                startDiscovery()
            }
            ACTION_STOP -> {
                stopDiscovery()
            }
            ACTION_CONNECT -> {
                val ip = intent.getStringExtra(EXTRA_DEVICE_IP) ?: return START_STICKY
                val port = intent.getIntExtra(EXTRA_DEVICE_PORT, 55556)
                val name = intent.getStringExtra(EXTRA_DEVICE_NAME) ?: "Unknown"
                val device = Device(name = name, ipAddress = ip, port = port, platform = "Windows")
                connectToDevice(device)
            }
        }
        return START_NOT_STICKY
    }

    private fun startDiscovery() {
        if (networkDiscovery != null) return
        initializeNetworkDiscovery()
    }

    private fun stopDiscovery() {
        networkDiscovery?.stopDiscovery()
        connectionManager?.disconnect()
        stopForeground(true)
        stopSelf()
    }

    private fun initializeNetworkDiscovery() {
        networkDiscovery = NetworkDiscovery(
            context = this,
            onDeviceDiscovered = { device ->
                broadcast(ACTION_DEVICE_DISCOVERED) {
                    putExtra(EXTRA_DEVICE_IP, device.ipAddress)
                    putExtra(EXTRA_DEVICE_PORT, device.port)
                    putExtra(EXTRA_DEVICE_NAME, device.name)
                }
            },
            onDeviceLost = { device ->
                broadcast(ACTION_DEVICE_LOST) {
                    putExtra(EXTRA_DEVICE_IP, device.ipAddress)
                    putExtra(EXTRA_DEVICE_NAME, device.name)
                }
            }
        )

        networkDiscovery?.startDiscovery()
    }

    private fun connectToDevice(device: Device) {
        if (isConnecting) {
            Log.w(TAG, "Already connecting, ignoring duplicate request")
            return
        }
        isConnecting = true

        connectionManager?.disconnect()
        connectedDevice = device

        connectionManager = ConnectionManager(
            onConnected = {
                isConnecting = false
                Log.i(TAG, "Connected to ${device.name}")
                updateNotification("Connected to ${device.name}")
                broadcast(ACTION_CONNECTION_STATUS_CHANGED) {
                    putExtra(EXTRA_CONNECTED, true)
                    putExtra(EXTRA_DEVICE_IP, device.ipAddress)
                    putExtra(EXTRA_DEVICE_PORT, device.port)
                    putExtra(EXTRA_DEVICE_NAME, device.name)
                }
            },
            onDisconnected = {
                isConnecting = false
                updateNotification("Disconnected")
                broadcast(ACTION_CONNECTION_STATUS_CHANGED) {
                    putExtra(EXTRA_CONNECTED, false)
                }
            },
            onMessageReceived = { message ->
                broadcast(ACTION_MESSAGE_RECEIVED) {
                    putExtra(EXTRA_MESSAGE, message)
                }
            },
            onConnectionError = { error ->
                isConnecting = false
                Log.e(TAG, "Connection error: $error")
                broadcast(ACTION_CONNECTION_ERROR) {
                    putExtra(EXTRA_ERROR, error)
                }
            }
        )

        connectionManager?.connect(device)
    }

    private fun broadcast(action: String, extras: Intent.() -> Unit) {
        val intent = Intent(action).apply {
            setPackage(packageName)
            extras()
        }
        sendBroadcast(intent)
    }

    fun sendMessage(message: String) {
        connectionManager?.sendMessage(message)
    }

    fun isConnected(): Boolean = connectionManager?.isConnected() ?: false

    private fun createNotificationChannel() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val channel = NotificationChannel(
                CHANNEL_ID,
                "Ecosystem Service",
                NotificationManager.IMPORTANCE_LOW
            ).apply {
                description = "Device ecosystem background service"
            }
            val notificationManager = getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
            notificationManager.createNotificationChannel(channel)
        }
    }

    private fun createNotification(): Notification {
        val pendingIntent = PendingIntent.getActivity(
            this, 0,
            Intent(this, MainActivity::class.java),
            PendingIntent.FLAG_IMMUTABLE
        )
        return NotificationCompat.Builder(this, CHANNEL_ID)
            .setContentTitle("Device Ecosystem")
            .setContentText("Service is running")
            .setSmallIcon(R.drawable.ic_connected)
            .setContentIntent(pendingIntent)
            .setOngoing(true)
            .build()
    }

    private fun updateNotification(message: String) {
        try {
            val notification = NotificationCompat.Builder(this, CHANNEL_ID)
                .setContentTitle("Device Ecosystem")
                .setContentText(message)
                .setSmallIcon(R.drawable.ic_connected)
                .build()
            val notificationManager = getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
            notificationManager.notify(NOTIFICATION_ID, notification)
        } catch (e: Exception) {
            Log.e(TAG, "Failed to update notification", e)
        }
    }

    override fun onBind(intent: Intent?): IBinder? = null

    companion object {
        const val ACTION_START = "com.ecosystem.android.START_SERVICE"
        const val ACTION_STOP = "com.ecosystem.android.STOP_SERVICE"
        const val ACTION_CONNECT = "com.ecosystem.android.CONNECT_TO_DEVICE"
        const val ACTION_DEVICE_DISCOVERED = "com.ecosystem.android.DEVICE_DISCOVERED"
        const val ACTION_DEVICE_LOST = "com.ecosystem.android.DEVICE_LOST"
        const val ACTION_CONNECTION_STATUS_CHANGED = "com.ecosystem.android.CONNECTION_STATUS_CHANGED"
        const val ACTION_MESSAGE_RECEIVED = "com.ecosystem.android.MESSAGE_RECEIVED"
        const val ACTION_CONNECTION_ERROR = "com.ecosystem.android.CONNECTION_ERROR"

        const val EXTRA_DEVICE_IP = "device_ip"
        const val EXTRA_DEVICE_PORT = "device_port"
        const val EXTRA_DEVICE_NAME = "device_name"
        const val EXTRA_CONNECTED = "connected"
        const val EXTRA_MESSAGE = "message"
        const val EXTRA_ERROR = "error"
    }
}
