import React, { useEffect, useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import { useLanguage } from '../../context/LanguageContext';
import { api, ApiError } from '../../services/apiClient';
import { CameraScanner } from '../../components/CameraScanner';
import type { StudentAttendanceHistoryItem, StudentAttendanceSummary } from '../../types';

export const StudentDashboard: React.FC = () => {
  const { user } = useAuth();
  const { t } = useLanguage();

  const [summary, setSummary] = useState<StudentAttendanceSummary | null>(null);
  const [history, setHistory] = useState<StudentAttendanceHistoryItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [showCamera, setShowCamera] = useState(false);
  const [showManualInput, setShowManualInput] = useState(false);
  const [qrTokenInput, setQrTokenInput] = useState('');
  const [checkInMsg, setCheckInMsg] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [checkingIn, setCheckingIn] = useState(false);

  const loadData = async () => {
    try {
      const [sumRes, histRes] = await Promise.all([
        api.get<StudentAttendanceSummary>('/student/attendance/summary'),
        api.get<StudentAttendanceHistoryItem[]>('/student/attendance/history'),
      ]);
      setSummary(sumRes);
      setHistory(histRes);
    } catch (err) {
      console.error('Failed to load student attendance', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const handleProcessToken = async (rawToken: string) => {
    if (!rawToken || !rawToken.trim()) return;

    let token = rawToken.trim();
    // In case the scanned QR is a URL containing token query parameter
    if (token.includes('token=')) {
      const match = token.match(/token=([^&]+)/);
      if (match) token = match[1];
    }

    setCheckInMsg(null);
    setCheckingIn(true);

    try {
      const res = await api.post<{ message: string; sessionTitle: string; groupName: string }>(
        '/student/attendance/check-in',
        { qrToken: token }
      );
      setCheckInMsg({
        type: 'success',
        text: `🎉 تم تسجيل حضورك بنجاح في: ${res.sessionTitle} (${res.groupName})`,
      });
      setQrTokenInput('');
      setShowCamera(false);
      await loadData();
    } catch (err) {
      if (err instanceof ApiError) {
        setCheckInMsg({ type: 'error', text: err.message });
      } else {
        setCheckInMsg({ type: 'error', text: 'تعذر التحقق من رمز الحضور، يرجى إعادة مسح الرمز' });
      }
    } finally {
      setCheckingIn(false);
    }
  };

  const handleManualCheckIn = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!qrTokenInput.trim()) return;
    await handleProcessToken(qrTokenInput);
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.75rem' }}>
      {/* Student Welcome Header */}
      <div className="card" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '1rem' }}>
        <div>
          <h2 style={{ fontSize: '1.4rem' }}>{t('welcome')}، {user?.fullName}</h2>
          <p style={{ fontWeight: 600, color: 'var(--primary-accent)', marginTop: '0.2rem' }}>
            كود الطالب: {user?.studentCode}
          </p>
        </div>

        {summary && (
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
            <div style={{ textAlign: 'center' }}>
              <span style={{ fontSize: '0.85rem', color: 'var(--text-muted)' }}>{t('myPercentage')}</span>
              <div style={{ fontSize: '1.8rem', fontWeight: 800, color: summary.attendancePercentage >= 80 ? 'var(--status-present)' : 'var(--status-late)' }}>
                {summary.attendancePercentage}%
              </div>
            </div>
          </div>
        )}
      </div>

      {/* QR Check-In Section */}
      <div className="card" style={{ border: '2px solid var(--primary-accent)', position: 'relative', overflow: 'hidden' }}>
        <h3 style={{ marginBottom: '0.4rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <span>📷</span> مسح باركود الحضور المباشر
        </h3>
        <p style={{ fontSize: '0.9rem', marginBottom: '1.25rem', color: 'var(--text-secondary)' }}>
          افتح الكاميرا ووجّهها نحو باركود الشاشة لدى المعلم لتسجيل حضورك تلقائياً وبشكل فوري.
        </p>

        {checkInMsg && (
          <div
            style={{
              padding: '0.85rem 1.25rem',
              borderRadius: 'var(--border-radius-md)',
              backgroundColor: checkInMsg.type === 'success' ? 'var(--status-present-subtle)' : 'var(--status-absent-subtle)',
              color: checkInMsg.type === 'success' ? 'var(--status-present)' : 'var(--status-absent)',
              marginBottom: '1.25rem',
              fontWeight: 700,
              fontSize: '1rem',
              border: `1px solid ${checkInMsg.type === 'success' ? 'var(--status-present)' : 'var(--status-absent)'}`,
              display: 'flex',
              alignItems: 'center',
              gap: '0.5rem',
            }}
          >
            <span>{checkInMsg.type === 'success' ? '✅' : '⚠️'}</span>
            <span>{checkInMsg.text}</span>
          </div>
        )}

        {showCamera ? (
          <CameraScanner
            onScan={handleProcessToken}
            isProcessing={checkingIn}
            onClose={() => setShowCamera(false)}
          />
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
            <button
              type="button"
              onClick={() => {
                setCheckInMsg(null);
                setShowCamera(true);
              }}
              className="btn btn-primary"
              style={{
                padding: '1rem 1.5rem',
                fontSize: '1.1rem',
                fontWeight: 700,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: '0.75rem',
                borderRadius: 'var(--border-radius-lg)',
                boxShadow: '0 6px 20px rgba(197, 155, 39, 0.35)',
              }}
            >
              <span style={{ fontSize: '1.4rem' }}>📷</span>
              <span>فتح الكاميرا ومسح الـ QR الآن</span>
            </button>

            {/* Toggle Manual Fallback */}
            <div style={{ textAlign: 'center', marginTop: '0.25rem' }}>
              <button
                type="button"
                onClick={() => setShowManualInput(!showManualInput)}
                style={{
                  background: 'none',
                  border: 'none',
                  color: 'var(--text-muted)',
                  fontSize: '0.85rem',
                  cursor: 'pointer',
                  textDecoration: 'underline',
                }}
              >
                {showManualInput ? 'إخفاء الإدخال اليدوي' : 'تواجه مشكلة في الكاميرا؟ أدخل الرمز يدوياً'}
              </button>
            </div>

            {showManualInput && (
              <form onSubmit={handleManualCheckIn} style={{ display: 'flex', gap: '0.75rem', flexWrap: 'wrap', marginTop: '0.5rem' }}>
                <input
                  type="text"
                  className="form-input"
                  placeholder="أدخل رمز الـ QR أو الصقه هنا..."
                  value={qrTokenInput}
                  onChange={e => setQrTokenInput(e.target.value)}
                  style={{ flex: 1, minWidth: '240px' }}
                />
                <button
                  type="submit"
                  className="btn btn-secondary"
                  disabled={checkingIn || !qrTokenInput.trim()}
                  style={{ minWidth: '140px' }}
                >
                  {checkingIn ? 'جاري التحقق...' : 'تسجيل يدوي'}
                </button>
              </form>
            )}
          </div>
        )}
      </div>

      {/* Metrics Row */}
      {summary && (
        <div className="metrics-grid">
          <div className="metric-card">
            <span className="metric-label">{t('totalSessions')}</span>
            <span className="metric-value">{summary.totalSessions}</span>
          </div>
          <div className="metric-card">
            <span className="metric-label" style={{ color: 'var(--status-present)' }}>{t('present')}</span>
            <span className="metric-value" style={{ color: 'var(--status-present)' }}>{summary.presentCount}</span>
          </div>
          <div className="metric-card">
            <span className="metric-label" style={{ color: 'var(--status-late)' }}>{t('late')}</span>
            <span className="metric-value" style={{ color: 'var(--status-late)' }}>{summary.lateCount}</span>
          </div>
          <div className="metric-card">
            <span className="metric-label" style={{ color: 'var(--status-excused)' }}>{t('excused')}</span>
            <span className="metric-value" style={{ color: 'var(--status-excused)' }}>{summary.excusedCount}</span>
          </div>
          <div className="metric-card">
            <span className="metric-label" style={{ color: 'var(--status-absent)' }}>{t('absent')}</span>
            <span className="metric-value" style={{ color: 'var(--status-absent)' }}>{summary.absentCount}</span>
          </div>
        </div>
      )}

      {/* Attendance History Timeline */}
      <div className="card">
        <h3 style={{ marginBottom: '1.25rem' }}>{t('myAttendance')}</h3>

        {loading ? (
          <p>جاري تحميل السجلات...</p>
        ) : history.length === 0 ? (
          <p style={{ textAlign: 'center', padding: '2rem', color: 'var(--text-muted)' }}>
            لا توجد سجلات حضور سابقة حتى الآن.
          </p>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.85rem' }}>
            {history.map(item => (
              <div
                key={item.attendanceRecordId}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  padding: '1rem',
                  borderRadius: 'var(--border-radius-md)',
                  backgroundColor: 'var(--bg-secondary)',
                  border: '1px solid var(--border-color)',
                  flexWrap: 'wrap',
                  gap: '0.75rem',
                }}
              >
                <div>
                  <h4 style={{ fontSize: '1.05rem', marginBottom: '0.2rem' }}>{item.sessionTitle}</h4>
                  <p style={{ fontSize: '0.85rem' }}>
                    {item.subjectName} — {item.groupName} | {new Date(item.sessionDateUtc).toLocaleDateString('ar-EG', { dateStyle: 'medium' })}
                  </p>
                </div>

                <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
                  {item.minutesLate && item.minutesLate > 0 && (
                    <span style={{ fontSize: '0.85rem', color: 'var(--status-late)', fontWeight: 600 }}>
                      تأخير {item.minutesLate} دقيقة
                    </span>
                  )}
                  <span
                    className={`badge ${
                      item.status === 'Present'
                        ? 'badge-present'
                        : item.status === 'Late'
                        ? 'badge-late'
                        : item.status === 'Excused'
                        ? 'badge-excused'
                        : 'badge-absent'
                    }`}
                  >
                    {t(item.status.toLowerCase())}
                  </span>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};
