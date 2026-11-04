import React, { useEffect, useState } from 'react';
import { useLanguage } from '../../context/LanguageContext';
import { api } from '../../services/apiClient';
import type { AcademicGroup, AtRiskStudent, GroupAttendanceMatrix } from '../../types';

export const ReportsView: React.FC = () => {
  const { t } = useLanguage();

  const [activeTab, setActiveTab] = useState<'at-risk' | 'matrix'>('at-risk');
  const [groups, setGroups] = useState<AcademicGroup[]>([]);
  const [selectedGroupId, setSelectedGroupId] = useState<string>('');
  const [atRiskList, setAtRiskList] = useState<AtRiskStudent[]>([]);
  const [matrix, setMatrix] = useState<GroupAttendanceMatrix | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([
      api.get<AtRiskStudent[]>('/teacher/reports/at-risk?minAbsences=3'),
      api.get<AcademicGroup[]>('/teacher/groups'),
    ])
      .then(([atRiskRes, groupsRes]) => {
        setAtRiskList(atRiskRes);
        setGroups(groupsRes);
        if (groupsRes.length > 0) {
          setSelectedGroupId(groupsRes[0].id);
        }
      })
      .catch(console.error)
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    if (activeTab === 'matrix' && selectedGroupId) {
      setLoading(true);
      api.get<GroupAttendanceMatrix>(`/teacher/reports/groups/${selectedGroupId}/matrix`)
        .then(setMatrix)
        .catch(console.error)
        .finally(() => setLoading(false));
    }
  }, [activeTab, selectedGroupId]);

  const handleDownloadFile = async (url: string, defaultFilename: string) => {
    try {
      const blob = await api.get<Blob>(url);
      const downloadUrl = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = downloadUrl;
      a.download = defaultFilename;
      document.body.appendChild(a);
      a.click();
      a.remove();
      window.URL.revokeObjectURL(downloadUrl);
    } catch (err) {
      console.error('Download failed', err);
      alert('فشل تنزيل ملف التصدير');
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
      {/* Header & Export Center */}
      <div className="card" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '1rem' }}>
        <div>
          <h2 style={{ fontSize: '1.4rem' }}>{t('reports')}</h2>
          <p style={{ fontSize: '0.88rem' }}>تحليلات التزام الطلاب، كشوف الحضور الشهرية والتصدير المباشر لـ Excel</p>
        </div>

        <div style={{ display: 'flex', gap: '0.6rem', flexWrap: 'wrap' }}>
          <button
            type="button"
            className="btn btn-secondary"
            onClick={() => handleDownloadFile('/teacher/reports/export/students', 'Students_Roster.csv')}
          >
            <span>📥</span> تصدير سجل الطلاب (Excel)
          </button>
          {selectedGroupId && (
            <button
              type="button"
              className="btn btn-primary"
              onClick={() => handleDownloadFile(`/teacher/reports/export/group-attendance?groupId=${selectedGroupId}`, 'Group_Attendance.csv')}
            >
              <span>📊</span> تصدير حضور المجموعة
            </button>
          )}
        </div>
      </div>

      {/* Tabs */}
      <div style={{ display: 'flex', gap: '0.5rem', borderBottom: '1px solid var(--border-color)', paddingBottom: '0.5rem' }}>
        <button
          type="button"
          className="btn"
          style={{
            backgroundColor: activeTab === 'at-risk' ? 'var(--primary-accent)' : 'transparent',
            color: activeTab === 'at-risk' ? 'var(--primary-accent-contrast)' : 'var(--text-muted)',
            minHeight: '40px',
          }}
          onClick={() => setActiveTab('at-risk')}
        >
          ⚠️ الطلاب المعرضين للغياب والرسوب ({atRiskList.length})
        </button>
        <button
          type="button"
          className="btn"
          style={{
            backgroundColor: activeTab === 'matrix' ? 'var(--primary-accent)' : 'transparent',
            color: activeTab === 'matrix' ? 'var(--primary-accent-contrast)' : 'var(--text-muted)',
            minHeight: '40px',
          }}
          onClick={() => setActiveTab('matrix')}
        >
          📅 مصفوفة حضور المجموعة (Attendance Matrix)
        </button>
      </div>

      {/* Tab 1: At-Risk Students */}
      {activeTab === 'at-risk' && (
        <div className="card">
          <h3 style={{ marginBottom: '0.5rem', color: 'var(--status-absent)' }}>
            قائمة الطلاب ذوي الغياب المتكرر (أكثر من 3 مرات غياب أو نسبة حضور أقل من 75%)
          </h3>
          <p style={{ fontSize: '0.9rem', marginBottom: '1.25rem' }}>
            تواصل فوري مع أولياء الأمور لتنبيههم قبل تراكم الدروس.
          </p>

          {atRiskList.length === 0 ? (
            <div style={{ textAlign: 'center', padding: '3rem', color: 'var(--status-present)' }}>
              🎉 رائع! لا يوجد طلاب معرضون للخطر حاليًا، جميع الطلاب ملتزمون بنسبة حضور ممتازة.
            </div>
          ) : (
            <div style={{ overflowX: 'auto' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'start' }}>
                <thead>
                  <tr style={{ borderBottom: '2px solid var(--border-color)', color: 'var(--text-secondary)' }}>
                    <th style={{ padding: '0.75rem', textAlign: 'start' }}>اسم الطالب</th>
                    <th style={{ padding: '0.75rem', textAlign: 'start' }}>المجموعة</th>
                    <th style={{ padding: '0.75rem', textAlign: 'center' }}>مرات الغياب</th>
                    <th style={{ padding: '0.75rem', textAlign: 'center' }}>نسبة الحضور</th>
                    <th style={{ padding: '0.75rem', textAlign: 'start' }}>هاتف ولي الأمر</th>
                    <th style={{ padding: '0.75rem', textAlign: 'start' }}>آخر حضور</th>
                  </tr>
                </thead>
                <tbody>
                  {atRiskList.map(st => (
                    <tr key={st.studentId} style={{ borderBottom: '1px solid var(--border-color-subtle)' }}>
                      <td style={{ padding: '0.85rem', fontWeight: 700 }}>
                        {st.fullName}
                        <div style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>{st.studentCode}</div>
                      </td>
                      <td style={{ padding: '0.85rem' }}>{st.groupName}</td>
                      <td style={{ padding: '0.85rem', textAlign: 'center', fontWeight: 800, color: 'var(--status-absent)' }}>
                        {st.absentSessions} غياب
                      </td>
                      <td style={{ padding: '0.85rem', textAlign: 'center' }}>
                        <span className="badge badge-absent" style={{ fontSize: '0.9rem' }}>
                          {st.attendanceRate}%
                        </span>
                      </td>
                      <td style={{ padding: '0.85rem', direction: 'ltr', textAlign: 'end', fontWeight: 600 }}>
                        {st.parentPhoneNumber || st.phoneNumber || '—'}
                      </td>
                      <td style={{ padding: '0.85rem', fontSize: '0.85rem' }}>
                        {st.lastAttendedDateUtc
                          ? new Date(st.lastAttendedDateUtc).toLocaleDateString('ar-EG')
                          : 'لم يحضر بعد'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {/* Tab 2: Attendance Matrix */}
      {activeTab === 'matrix' && (
        <div className="card">
          <div style={{ display: 'flex', alignItems: 'center', gap: '1rem', marginBottom: '1.25rem', flexWrap: 'wrap' }}>
            <label className="form-label" style={{ margin: 0 }}>اختر المجموعة:</label>
            <select
              className="form-select"
              value={selectedGroupId}
              onChange={e => setSelectedGroupId(e.target.value)}
              style={{ minWidth: '240px' }}
            >
              {groups.map(g => (
                <option key={g.id} value={g.id}>
                  {g.name} ({g.subjectName})
                </option>
              ))}
            </select>
          </div>

          {loading ? (
            <p>جاري تحميل مصفوفة الحضور...</p>
          ) : !matrix || matrix.students.length === 0 ? (
            <p style={{ textAlign: 'center', padding: '2rem', color: 'var(--text-muted)' }}>
              لا توجد بيانات حضور مسجلة لهذه المجموعة حتى الآن.
            </p>
          ) : (
            <div style={{ overflowX: 'auto' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.9rem' }}>
                <thead>
                  <tr style={{ borderBottom: '2px solid var(--border-color)', backgroundColor: 'var(--bg-secondary)' }}>
                    <th style={{ padding: '0.75rem', textAlign: 'start', position: 'sticky', right: 0, backgroundColor: 'var(--bg-secondary)', zIndex: 10 }}>
                      اسم الطالب
                    </th>
                    {matrix.sessions.map(sess => (
                      <th key={sess.sessionId} style={{ padding: '0.6rem', textAlign: 'center', minWidth: '70px' }}>
                        <div style={{ fontWeight: 700 }}>{sess.title}</div>
                        <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>
                          {new Date(sess.sessionDateUtc).toLocaleDateString('ar-EG', { month: 'numeric', day: 'numeric' })}
                        </div>
                      </th>
                    ))}
                    <th style={{ padding: '0.75rem', textAlign: 'center' }}>النسبة</th>
                  </tr>
                </thead>
                <tbody>
                  {matrix.students.map(row => (
                    <tr key={row.studentId} style={{ borderBottom: '1px solid var(--border-color-subtle)' }}>
                      <td style={{ padding: '0.75rem', fontWeight: 600, position: 'sticky', right: 0, backgroundColor: 'var(--bg-surface)', zIndex: 5 }}>
                        {row.fullName}
                      </td>
                      {matrix.sessions.map(sess => {
                        const status = row.sessionStatuses[sess.sessionId] || '-';
                        return (
                          <td key={sess.sessionId} style={{ padding: '0.6rem', textAlign: 'center' }}>
                            <span
                              style={{
                                display: 'inline-block',
                                width: '28px',
                                height: '28px',
                                lineHeight: '28px',
                                borderRadius: '6px',
                                fontWeight: 800,
                                fontSize: '0.8rem',
                                backgroundColor:
                                  status === 'Present' ? 'var(--status-present-subtle)' :
                                  status === 'Late' ? 'var(--status-late-subtle)' :
                                  status === 'Excused' ? 'var(--status-excused-subtle)' :
                                  status === 'Absent' ? 'var(--status-absent-subtle)' : 'transparent',
                                color:
                                  status === 'Present' ? 'var(--status-present)' :
                                  status === 'Late' ? 'var(--status-late)' :
                                  status === 'Excused' ? 'var(--status-excused)' :
                                  status === 'Absent' ? 'var(--status-absent)' : 'var(--text-muted)',
                              }}
                            >
                              {status === 'Present' ? 'P' : status === 'Late' ? 'L' : status === 'Excused' ? 'E' : status === 'Absent' ? 'A' : '-'}
                            </span>
                          </td>
                        );
                      })}
                      <td style={{ padding: '0.75rem', textAlign: 'center', fontWeight: 700 }}>
                        {row.attendanceRate}%
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}
    </div>
  );
};
