import React, { useEffect, useRef, useState } from 'react';
import { Html5Qrcode, type Html5QrcodeCameraScanConfig } from 'html5-qrcode';

interface CameraScannerProps {
  onScan: (decodedText: string) => void;
  isProcessing?: boolean;
  onClose: () => void;
}

export const CameraScanner: React.FC<CameraScannerProps> = ({ onScan, isProcessing, onClose }) => {
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [cameras, setCameras] = useState<{ id: string; label: string }[]>([]);
  const [selectedCameraId, setSelectedCameraId] = useState<string>('');
  const [isScannerRunning, setIsScannerRunning] = useState(false);

  const scannerRef = useRef<Html5Qrcode | null>(null);
  const containerId = 'hudoor-qr-scanner-viewport';
  const hasScannedRef = useRef(false);

  // Audio confirmation feedback
  const playSuccessChime = () => {
    try {
      const AudioCtx = window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
      if (!AudioCtx) return;
      const ctx = new AudioCtx();
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();

      osc.type = 'sine';
      osc.frequency.setValueAtTime(587.33, ctx.currentTime); // D5
      osc.frequency.setValueAtTime(880, ctx.currentTime + 0.08); // A5
      gain.gain.setValueAtTime(0.2, ctx.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.3);

      osc.connect(gain);
      gain.connect(ctx.destination);
      osc.start();
      osc.stop(ctx.currentTime + 0.3);
    } catch {
      // Audio autoplay restrictions or unsupported
    }
  };

  const stopScanner = async () => {
    if (scannerRef.current) {
      try {
        if (scannerRef.current.isScanning) {
          await scannerRef.current.stop();
        }
        scannerRef.current.clear();
      } catch (err) {
        console.error('Error stopping QR scanner', err);
      } finally {
        scannerRef.current = null;
        setIsScannerRunning(false);
      }
    }
  };

  const startScanning = async (cameraSource: string | MediaTrackConstraints) => {
    try {
      await stopScanner();

      const scanner = new Html5Qrcode(containerId);
      scannerRef.current = scanner;

      const config: Html5QrcodeCameraScanConfig = {
        fps: 12,
        qrbox: (viewfinderWidth, viewfinderHeight) => {
          const edge = Math.min(viewfinderWidth, viewfinderHeight) * 0.72;
          return { width: Math.floor(edge), height: Math.floor(edge) };
        },
        aspectRatio: 1.0,
      };

      await scanner.start(
        cameraSource,
        config,
        (decodedText) => {
          if (hasScannedRef.current || isProcessing) return;
          hasScannedRef.current = true;

          playSuccessChime();
          if (navigator.vibrate) {
            navigator.vibrate([100, 50, 100]);
          }

          onScan(decodedText);
          stopScanner();
        },
        () => {
          // frame parse drop - normal
        }
      );

      setIsScannerRunning(true);
      setErrorMsg(null);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : String(err);
      if (msg.includes('Permission') || msg.includes('NotAllowedError')) {
        setErrorMsg('يرجى السماح للمتصفح بالوصول إلى الكاميرا من إعدادات الموقع.');
      } else {
        setErrorMsg('حدث خطأ أثناء تشغيل الكاميرا: ' + msg);
      }
      setIsScannerRunning(false);
    }
  };

  useEffect(() => {
    let isMounted = true;
    hasScannedRef.current = false;

    async function initCamera() {
      try {
        const devices = await Html5Qrcode.getCameras();
        if (!isMounted) return;

        if (devices && devices.length > 0) {
          setCameras(devices);
          // Prefer environment / back camera
          const backCam = devices.find(d => 
            d.label.toLowerCase().includes('back') || 
            d.label.toLowerCase().includes('environment') ||
            d.label.toLowerCase().includes('خلفية')
          );
          const chosenId = backCam ? backCam.id : devices[0].id;
          setSelectedCameraId(chosenId);
          startScanning(chosenId);
        } else {
          // Try default facing mode
          startScanning({ facingMode: 'environment' });
        }
      } catch (err: unknown) {
        if (!isMounted) return;
        const msg = err instanceof Error ? err.message : String(err);
        if (msg.includes('Permission') || msg.includes('NotAllowedError')) {
          setErrorMsg('يرجى السماح للمتصفح بالوصول إلى الكاميرا لمسح باركود الحضور.');
        } else {
          setErrorMsg('تعذر العثور على كاميرا متصلة. يمكنك إدخال الرمز يدوياً.');
        }
      }
    }

    initCamera();

    return () => {
      isMounted = false;
      stopScanner();
    };
  }, []);

  const handleCameraChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    const newId = e.target.value;
    setSelectedCameraId(newId);
    startScanning(newId);
  };

  return (
    <div style={{
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      backgroundColor: 'var(--bg-secondary)',
      borderRadius: 'var(--border-radius-lg)',
      padding: '1.25rem',
      gap: '1rem',
      position: 'relative',
      border: '1px solid var(--border-color)',
    }}>
      {/* Top Header Controls */}
      <div style={{
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        width: '100%',
        maxWidth: '480px',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', fontWeight: 700 }}>
          <span style={{ fontSize: '1.2rem', color: 'var(--primary-accent)' }}>📷</span>
          <span>ماسح باركود الحضور المباشر</span>
        </div>

        <button
          type="button"
          onClick={onClose}
          className="btn"
          style={{
            padding: '0.35rem 0.75rem',
            fontSize: '0.85rem',
            backgroundColor: 'var(--bg-surface)',
            color: 'var(--text-secondary)',
            border: '1px solid var(--border-color)',
          }}
        >
          ✕ إغلاق الكاميرا
        </button>
      </div>

      {/* Camera Selection (if multiple) */}
      {cameras.length > 1 && (
        <div style={{ width: '100%', maxWidth: '480px' }}>
          <label style={{ fontSize: '0.8rem', color: 'var(--text-muted)', marginBottom: '0.25rem', display: 'block' }}>
            تبديل الكاميرا:
          </label>
          <select
            className="form-input"
            value={selectedCameraId}
            onChange={handleCameraChange}
            style={{ fontSize: '0.85rem', padding: '0.4rem 0.75rem' }}
          >
            {cameras.map(c => (
              <option key={c.id} value={c.id}>
                {c.label || `كاميرا (${c.id.substring(0, 5)})`}
              </option>
            ))}
          </select>
        </div>
      )}

      {/* Error message */}
      {errorMsg && (
        <div style={{
          backgroundColor: 'var(--status-absent-subtle)',
          color: 'var(--status-absent)',
          padding: '0.75rem 1rem',
          borderRadius: 'var(--border-radius-md)',
          fontSize: '0.9rem',
          width: '100%',
          maxWidth: '480px',
          textAlign: 'center',
          fontWeight: 600,
        }}>
          {errorMsg}
        </div>
      )}

      {/* Viewport Box */}
      <div style={{
        position: 'relative',
        width: '100%',
        maxWidth: '380px',
        aspectRatio: '1',
        overflow: 'hidden',
        borderRadius: 'var(--border-radius-lg)',
        backgroundColor: '#000000',
        boxShadow: '0 8px 24px rgba(0, 0, 0, 0.4)',
        border: '2px solid var(--primary-accent)',
      }}>
        {/* HTML5 QR Code Mount Div */}
        <div id={containerId} style={{ width: '100%', height: '100%' }} />

        {/* Viewfinder Target Overlay UI */}
        {isScannerRunning && (
          <div style={{
            position: 'absolute',
            inset: 0,
            pointerEvents: 'none',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
          }}>
            {/* Target Reticle Box */}
            <div style={{
              width: '72%',
              height: '72%',
              position: 'relative',
              boxShadow: '0 0 0 9999px rgba(0, 0, 0, 0.45)',
              borderRadius: '16px',
            }}>
              {/* Corner brackets */}
              <div style={{ position: 'absolute', top: 0, left: 0, width: '28px', height: '28px', borderTop: '4px solid var(--primary-accent)', borderLeft: '4px solid var(--primary-accent)', borderTopLeftRadius: '12px' }} />
              <div style={{ position: 'absolute', top: 0, right: 0, width: '28px', height: '28px', borderTop: '4px solid var(--primary-accent)', borderRight: '4px solid var(--primary-accent)', borderTopRightRadius: '12px' }} />
              <div style={{ position: 'absolute', bottom: 0, left: 0, width: '28px', height: '28px', borderBottom: '4px solid var(--primary-accent)', borderLeft: '4px solid var(--primary-accent)', borderBottomLeftRadius: '12px' }} />
              <div style={{ position: 'absolute', bottom: 0, right: 0, width: '28px', height: '28px', borderBottom: '4px solid var(--primary-accent)', borderRight: '4px solid var(--primary-accent)', borderBottomRightRadius: '12px' }} />

              {/* Animated Laser Scanning Line */}
              <div className="hudoor-scanner-laser" />
            </div>
          </div>
        )}

        {isProcessing && (
          <div style={{
            position: 'absolute',
            inset: 0,
            backgroundColor: 'rgba(11, 19, 41, 0.85)',
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            gap: '0.75rem',
            color: '#FFFFFF',
            zIndex: 10,
          }}>
            <div className="spinner" style={{ width: '36px', height: '36px', border: '3px solid rgba(255,255,255,0.2)', borderTopColor: 'var(--primary-accent)', borderRadius: '50%', animation: 'spin 0.8s linear infinite' }} />
            <span style={{ fontWeight: 600, fontSize: '0.95rem' }}>جاري تسجيل حضورك وتأكيد التوقيع الرقمي...</span>
          </div>
        )}
      </div>

      <p style={{
        fontSize: '0.85rem',
        color: 'var(--text-secondary)',
        textAlign: 'center',
        maxWidth: '400px',
      }}>
        وجّه كاميرا الهاتف أو الحاسوب مباشرة نحو رمز الـ QR المعروض على شاشة المعلم، وسيتم التحضير تلقائياً دون أي ضغط.
      </p>
    </div>
  );
};
