using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit;
using System; // para SelectEnterEventArgs/IXRSelectInteractor etc.

[RequireComponent(typeof(XRGrabInteractable))]
public class WeaponVR : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float bulletForce = 12f;

    [Header("Haptics")]
    [SerializeField] private float hapticAmplitude = 0.5f;
    [SerializeField] private float hapticDuration = 0.1f;
    [SerializeField] private AudioSource fireSound;

    [SerializeField] private float fireRate = 0.45f;
    private float lastFireTime;

    private XRGrabInteractable grabInteractable;

    // estado atual de posse
    private bool wasSelected = false;
    private InputDeviceCharacteristics currentHand = InputDeviceCharacteristics.Controller; // Left/Right será definido ao pegar

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
    }

    private void Update()
    {
        // Detecta transições de pegar/soltar sem eventos
        if (!wasSelected && grabInteractable.isSelected)
        {
            wasSelected = true;
            DetectCurrentHand();
        }
        else if (wasSelected && !grabInteractable.isSelected)
        {
            wasSelected = false;
            currentHand = InputDeviceCharacteristics.Controller; // limpa
        }

        if (!grabInteractable.isSelected) return; // só funciona quando está na mão

        // Lê o gatilho da mão que está segurando
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(currentHand, devices);

        foreach (var device in devices)
        {
            if (device.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue))
            {
                if (triggerValue > 0.8f && Time.time >= lastFireTime + fireRate)
                {
                    Fire();
                }
            }
        }
    }

    private void DetectCurrentHand()
    {
        // Usa o primeiro interactor que está segurando
        // (Toolkit 3.x expõe uma lista de IXRSelectInteractor)
        if (grabInteractable.interactorsSelecting.Count > 0)
        {
            var interactor = grabInteractable.interactorsSelecting[0];
            // Heurística segura: olhar o nome do interactor
            var n = interactor.transform.gameObject.name.ToLower();
            Debug.Log(n);
            if (n.Contains("right"))
                currentHand = InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller;
            else if (n.Contains("left"))
                currentHand = InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller;
            else
            {
                // fallback: tenta ambas e escolhe a que responder primeiro
                currentHand = InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller;
                // se quiser, você pode fazer um teste de triggerValue pra decidir entre Right/Left dinamicamente
            }
        }
    }

    private void Fire()
    {
        fireSound?.Play();
        lastFireTime = Time.time;

        var bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        if (bullet.TryGetComponent<Rigidbody>(out var rb))
            rb.AddForce(firePoint.forward * bulletForce, ForceMode.Impulse);

        SendHapticImpulse(currentHand, hapticAmplitude, hapticDuration);
    }

    private void SendHapticImpulse(InputDeviceCharacteristics hand, float amplitude, float duration)
    {
        var devices = new List<UnityEngine.XR.InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(hand, devices);

        foreach (var device in devices)
        {
            if (device.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
            {
                device.SendHapticImpulse(0, amplitude, duration);
            }
        }
    }
}
