import React, { createContext, useContext, useEffect, useState } from 'react';
import type { AuthResponse, AuthUser, UserRole } from '../types';

interface AuthContextType {
  user: AuthUser | null;
  token: string | null;
  isAuthenticated: boolean;
  login: (auth: AuthResponse) => void;
  logout: () => void;
  updateMustChangePassword: (mustChange: boolean) => void;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

function parseRoleFromToken(token: string | null): UserRole | null {
  if (!token) return null;
  try {
    const payloadBase64 = token.split('.')[1];
    if (!payloadBase64) return null;
    const json = atob(payloadBase64.replace(/-/g, '+').replace(/_/g, '/'));
    const payload = JSON.parse(json);
    return (payload.role || payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']) as UserRole;
  } catch {
    return null;
  }
}

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [token, setToken] = useState<string | null>(() => {
    return localStorage.getItem('hudoor_token');
  });

  const [user, setUser] = useState<AuthUser | null>(() => {
    const saved = localStorage.getItem('hudoor_user');
    const savedToken = localStorage.getItem('hudoor_token');
    if (saved) {
      try {
        const parsed = JSON.parse(saved);
        if (!parsed.role && savedToken) {
          const inferredRole = parseRoleFromToken(savedToken);
          if (inferredRole) {
            parsed.role = inferredRole;
            localStorage.setItem('hudoor_user', JSON.stringify(parsed));
          }
        }
        return parsed;
      } catch {
        return null;
      }
    }
    return null;
  });

  useEffect(() => {
    const handleAuthExpired = () => {
      setUser(null);
      setToken(null);
    };

    window.addEventListener('hudoor_auth_expired', handleAuthExpired);
    return () => window.removeEventListener('hudoor_auth_expired', handleAuthExpired);
  }, []);

  const login = (auth: AuthResponse) => {
    const role = (auth.role || auth.accountType || parseRoleFromToken(auth.token)) as UserRole;
    const authUser: AuthUser = {
      id: auth.teacherId || auth.studentId || auth.usernameOrCode || auth.username || auth.studentCode || '',
      username: auth.username || auth.usernameOrCode,
      studentCode: auth.studentCode || (role === 'Student' ? auth.usernameOrCode : undefined),
      fullName: auth.fullName,
      role: role,
      mustChangePassword: auth.mustChangePassword,
    };

    localStorage.setItem('hudoor_token', auth.token);
    localStorage.setItem('hudoor_user', JSON.stringify(authUser));

    setToken(auth.token);
    setUser(authUser);
  };

  const logout = () => {
    localStorage.removeItem('hudoor_token');
    localStorage.removeItem('hudoor_user');
    setToken(null);
    setUser(null);
  };

  const updateMustChangePassword = (mustChange: boolean) => {
    if (user) {
      const updated = { ...user, mustChangePassword: mustChange };
      setUser(updated);
      localStorage.setItem('hudoor_user', JSON.stringify(updated));
    }
  };

  return (
    <AuthContext.Provider
      value={{
        user,
        token,
        isAuthenticated: !!token && !!user,
        login,
        logout,
        updateMustChangePassword,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};
