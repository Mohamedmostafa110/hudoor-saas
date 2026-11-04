import React, { useEffect, useRef, useState } from 'react';
import QRCode from 'qrcode';
import { useLanguage } from '../../context/LanguageContext';
import { api } from '../../services/apiClient';
import type { AttendanceSession, RosterStudent, SessionQrToken } from '../../types';

interface LiveSessionViewProps {
  initialSessionId?: string;
  onNavigate: (view: string, contextId?: string) => void;
}

export const LiveSessionView: React.FC<LiveSessionViewProps> = ({ initialSessionId, onNavigate }) => {
  const { t } = useLanguage();
  const canvasRef = useRef<HTMLCanvasElement | null>(null);

  const [sessions, setSessions] = useState<AttendanceSession[]>([]);
  const [selectedSessionId, setSelectedSessionId] = useState<string>(initialSessionId || '');
  const [selectedSession, setSelectedSession] = useState<AttendanceSession | null>(null);
  const [, setQrData] = useState<SessionQrToken | null>(null);
  const [remainingSeconds, setRemainingSeconds] = useState<number>(20);
  const [roster, setRoster] = useState<RosterStudent[]>([]);
  const [loading, setLoading] = useState(true);
  const [closing, setClosing] = useState(false);

  // Load active sessions
  useEffect(() => {
    api.get<AttendanceSession[]>('/teacher/sessions?status=Active')
      .then(res => {
        setSessions(res);
        if (!selectedSessionId && res.length > 0) {
          setSelectedSessionId(res[0].id);
        }
      })
      .catch(console.error)
      .finally(() => setLoading(false));
  }, []);

  // Fetch session details, QR token, and roster
  const fetchSessionData = async () => {
    if (!selectedSessionId) return;

    try {
      const [sessRes, qrRes, rosterRes] = await Promise.all([
        api.get<AttendanceSession>(`/teacher/sessions/${selectedSessionId}`),
        api.get<SessionQrToken>(`/teacher/sessions/${selectedSessionId}/qr-token`),
        api.get<RosterStudent[]>(`/teacher/sessions/${selectedSessionId}/roster`),
      ]);

      setSelectedSession(sessRes);
      setQrData(qrRes);
      setRemainingSeconds(qrRes.remainingSeconds);
      setRoster(rosterRes);

      // Render QR to canvas
      if (canvasRef.current && qrRes.qrToken) {
        QRCode.toCanvas(canvasRef.current, qrRes.qrToken, {
          width: 280,
          margin: 2,
          color: {
            dark: '#000000',
            light: '#FFFFFF',
          },
        });
      }
    } catch (err) {
      console.error('Failed to fetch live session data', err);
    }
  };

  useEffect(() => {
    if (selectedSessionId) {
      fetchSessionData();
    }
  }, [selectedSessionId]);

  // Countdown and rotation timer
  useEffect(() => {
    if (!selectedSessionId) return;

    const timer = setInterval(() => {
      setRemainingSeconds(prev => {
        if (prev <= 1) {
          fetchSessionData(); // Rotate token
          return 20;
        }
        return prev - 1;
      });
    }, 1000);

    return () => clearInterval(timer);
  }, [selectedSessionId]);

  // Poll roster every 4 seconds to show live check-ins
  useEffect(() => {
    if (!selectedSessionId) return;

    const rosterPoll = setInterval(() => {
      api.get<RosterStudent[]>(`/teacher/sessions/${selectedSessionId}/roster`)
        .then(setRoster)
        .catch(console.error);
    }, 4000);

    return () => clearInterval(rosterPoll);
  }, [selectedSessionId]);

  const handleCloseSession = async () => {
    if (!selectedSessionId) return;
    if (!window.confirm('هل أنت متأكد من إنهاء الجلسة؟ سيتم حصر الطلاب غير الحاضرين وتسجيلهم كـ غائب تلقائيًا.')) {
      return;
    }

    setClosing(true);
    try {
      await api.patch(`/teacher/sessions/${selectedSessionId}/status`, { status: 'Closed' });
      alert(t('sessionClosedMsg'));
      onNavigate('dashboard');
    } catch (err) {
      console.error('Failed to close session', err);
    } finally {
      setClosing(false);
    }
  };

  const presentCount = roster.filter(r => r.status === 'Present').length;
  const lateCount = roster.filter(r => r.status === 'Late').length;
  const totalEnrolled = roster.length;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
      {/* Session Selection Bar */}
      <div className="card" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '1rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '1rem', flexWrap: 'wrap' }}>
          <label className="form-label" style={{ margin: 0 }}>اختر الجلسة النشطة:</label>
          <select
            className="form-select"
            value={selectedSessionId}
            onChange={e => setSelectedSessionId(e.target.value)}
            style={{ minWidth: '260px' }}
          >
            {sessions.map(s => (
              <option key={s.id} value={s.id}>
                {s.title} ({s.groupName})
              </option>
            ))}
          </select>
        </div>

        <div style={{ display: 'flex', gap: '0.75rem' }}>
          <button
            type="button"
            className="btn btn-secondary"
            onClick={() => onNavigate('fast-roster', selectedSessionId)}
          >
            التحضير السريع باللمس
          </button>
          <button
            type="button"
            className="btn btn-danger"
            onClick={handleCloseSession}
            disabled={closing || !selectedSessionId}
          >
            {closing ? 'جاري الإنهاء...' : t('closeSessionBtn')}
          </button>
        </div>
      </div>

      {loading ? (
        <p>جاري تحميل الجلسة...</p>
      ) : !selectedSessionId ? (
        <div className="card" style={{ textAlign: 'center', padding: '3rem' }}>
          <h3>لا توجد جلسات حضور نشطة حاليًا</h3>
          <p style={{ marginTop: '0.5rem', marginBottom: '1.5rem' }}>
            يمكنك إنشاء جلسة حضور جديدة من لوحة التحكم لعرض باركود الـ QR للطلاب.
          </p>
          <button type="button" className="btn btn-primary" onClick={() => onNavigate('dashboard')}>
            العودة للوحة التحكم
          </button>
        </div>
      ) : (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: '1.5rem' }}>
          {/* Main High-Contrast QR Projector Card */}
          <div className="qr-projector-card">
            <h2 style={{ fontSize: '1.6rem', marginBottom: '0.25rem' }}>
              {selectedSession?.title}
            </h2>
            <p style={{ color: 'var(--text-secondary)', fontWeight: 600 }}>
              {selectedSession?.subjectName} — {selectedSession?.groupName}
            </p>

            <div className="qr-canvas-wrapper">
              <canvas ref={canvasRef} style={{ display: 'block', maxWidth: '100%', height: 'auto' }} />
            </div>

            <div className="countdown-badge">
              <span>⏱️</span>
              <span>{t('tokenRotatingIn')} <strong>{remainingSeconds}</strong> {t('seconds')}</span>
            </div>

            <p style={{ marginTop: '1rem', fontSize: '0.9rem', color: 'var(--text-muted)' }}>
              يتجدد الرمز تلقائيًا كل 20 ثانية لمنع تصوير الشاشة وتمريرها للغائبين
            </p>
          </div>

          {/* Real-time Live Check-in Monitor */}
          <div className="card" style={{ display: 'flex', flexDirection: 'column' }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '1.25rem', borderBottom: '1px solid var(--border-color)', paddingBottom: '0.75rem' }}>
              <h3>المسجلون في القاعة الآن</h3>
              <div style={{ fontSize: '1.25rem', fontWeight: 800, color: 'var(--status-present)' }}>
                {presentCount + lateCount} / {totalEnrolled} حاضر
              </div>
            </div>

            <div style={{ flex: 1, maxHeight: '420px', overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
              {roster.filter(r => r.status === 'Present' || r.status === 'Late').length === 0 ? (
                <div style={{ textAlign: 'center', padding: '3rem 1rem', color: 'var(--text-muted)' }}>
                  في انتظار مسح الطلاب للباركود...
                </div>
              ) : (
                roster
                  .filter(r => r.status === 'Present' || r.status === 'Late')
                  .map(student => (
                    <div
                      key={student.studentId}
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        padding: '0.65rem 0.85rem',
                        borderRadius: 'var(--border-radius-sm)',
                        backgroundColor: 'var(--bg-secondary)',
                      }}
                    >
                      <div>
                        <span style={{ fontWeight: 700, fontSize: '0.95rem' }}>{student.fullName}</span>
                        <span style={{ fontSize: '0.8rem', color: 'var(--text-muted)', marginInlineStart: '0.5rem' }}>
                          ({student.studentCode})
                        </span>
                      </div>
                      <span className={`badge ${student.status === 'Present' ? 'badge-present' : 'badge-late'}`}>
                        {student.method === 'QRCode' ? 'QR ✓' : 'يدوي'}
                      </span>
                    </div>
                  ))
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
