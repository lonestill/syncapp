package com.ecosystem.android.network

import android.content.Context
import android.net.nsd.NsdManager
import com.ecosystem.android.model.Device
import com.ecosystem.android.model.DeviceStatus
import android.net.nsd.NsdServiceInfo
import android.util.Log

class NetworkDiscovery(
    private val context: Context,
    private val onDeviceDiscovered: (Device) -> Unit,
    private val onDeviceLost: (Device) -> Unit
) {
    private val TAG = "NetworkDiscovery"
    private val SERVICE_TYPE = "_myecosystem._tcp"

    private var nsdManager: NsdManager? = null
    private var discoveryListener: NsdManager.DiscoveryListener? = null
    private var isRunning = false
    private val resolvedByServiceName = mutableMapOf<String, Device>()

    init {
        initializeNsdManager()
    }

    private fun initializeNsdManager() {
        nsdManager = context.getSystemService(Context.NSD_SERVICE) as NsdManager
    }

    fun startDiscovery() {
        if (isRunning) return
        isRunning = true

        startNsdDiscovery()
    }

    private fun startNsdDiscovery() {
        discoveryListener = object : NsdManager.DiscoveryListener {
            override fun onDiscoveryStarted(serviceType: String) {
                Log.d(TAG, "Service discovery started: $serviceType")
            }

            override fun onServiceFound(service: NsdServiceInfo) {
                Log.d(TAG, "Service found: ${service.serviceName}")
                if (!isRunning) return
                if (!service.serviceType.startsWith(SERVICE_TYPE)) return
                nsdManager?.resolveService(service, object : NsdManager.ResolveListener {
                    override fun onServiceResolved(serviceInfo: NsdServiceInfo) {
                        val host = serviceInfo.host?.hostAddress ?: return
                        if (host == "127.0.0.1" || host.startsWith("169.254.")) return
                        val device = Device(
                            name = serviceInfo.serviceName,
                            ipAddress = host,
                            port = serviceInfo.port,
                            platform = "Windows",
                            status = DeviceStatus.DISCOVERED
                        )
                        resolvedByServiceName[serviceInfo.serviceName] = device
                        onDeviceDiscovered(device)
                    }

                    override fun onResolveFailed(serviceInfo: NsdServiceInfo, errorCode: Int) {
                        Log.e(TAG, "Resolve failed: $errorCode")
                    }
                })
            }

            override fun onServiceLost(service: NsdServiceInfo) {
                Log.d(TAG, "Service lost: ${service.serviceName}")
                val device = resolvedByServiceName.remove(service.serviceName)
                if (device != null) {
                    onDeviceLost(device.copy(status = DeviceStatus.DISCONNECTED))
                }
            }

            override fun onDiscoveryStopped(serviceType: String) {
                Log.d(TAG, "Discovery stopped: $serviceType")
            }

            override fun onStartDiscoveryFailed(serviceType: String, errorCode: Int) {
                Log.e(TAG, "Discovery start failed: $errorCode")
            }

            override fun onStopDiscoveryFailed(serviceType: String, errorCode: Int) {
                Log.e(TAG, "Discovery stop failed: $errorCode")
            }
        }

        nsdManager?.discoverServices(SERVICE_TYPE, NsdManager.PROTOCOL_DNS_SD, discoveryListener)
    }

    fun registerService() {
        // Intentionally no-op for Android client.
        // Advertising Android service causes self-discovery and duplicate noisy devices.
    }

    fun stopDiscovery() {
        isRunning = false
        resolvedByServiceName.clear()
        discoveryListener?.let {
            try {
                nsdManager?.stopServiceDiscovery(it)
            } catch (e: Exception) {
                Log.w(TAG, "stopServiceDiscovery failed: ${e.message}")
            }
        }
    }
}